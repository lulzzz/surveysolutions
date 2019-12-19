using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using Anemonis.AspNetCore.RequestDecompression;
using Autofac;
using Autofac.Extensions.DependencyInjection;
using AutoMapper;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WB.Core.BoundedContexts.Headquarters;
using WB.Core.BoundedContexts.Headquarters.EmailProviders;
using WB.Core.BoundedContexts.Headquarters.Implementation;
using WB.Core.BoundedContexts.Headquarters.Implementation.Synchronization;
using WB.Core.BoundedContexts.Headquarters.Storage;
using WB.Core.BoundedContexts.Headquarters.Users.UserPreloading;
using WB.Core.BoundedContexts.Headquarters.Views.DataExport;
using WB.Core.BoundedContexts.Headquarters.Views.InterviewHistory;
using WB.Core.BoundedContexts.Headquarters.Views.SampleImport;
using WB.Core.BoundedContexts.Headquarters.WebInterview;
using WB.Core.Infrastructure;
using WB.Core.Infrastructure.EventBus;
using WB.Core.Infrastructure.Modularity.Autofac;
using WB.Core.Infrastructure.Ncqrs;
using WB.Core.SharedKernels.DataCollection;
using WB.Core.SharedKernels.SurveyManagement.Web.Models;
using WB.Enumerator.Native.WebInterview;
using WB.Infrastructure.Native.Files;
using WB.Infrastructure.Native.Logging;
using WB.Infrastructure.Native.Storage.Postgre;
using WB.Persistence.Headquarters.Migrations.Events;
using WB.Persistence.Headquarters.Migrations.Logs;
using WB.Persistence.Headquarters.Migrations.PlainStore;
using WB.Persistence.Headquarters.Migrations.ReadSide;
using WB.Persistence.Headquarters.Migrations.Users;
using WB.UI.Designer.CommonWeb;
using WB.UI.Headquarters.Code;
using WB.UI.Headquarters.Code.Authentication;
using WB.UI.Headquarters.Configs;
using WB.UI.Headquarters.Controllers.Api.PublicApi;
using WB.UI.Headquarters.Filters;
using WB.UI.Shared.Web.Captcha;
using WB.UI.Shared.Web.Configuration;
using WB.UI.Shared.Web.Versions;

namespace WB.UI.Headquarters
{
    public class Startup
    {
        private AutofacKernel autofacKernel;

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // ConfigureContainer is where you can register things directly
        // with Autofac. This runs after ConfigureServices so the things
        // here will override registrations made in ConfigureServices.
        // Don't build the container; that gets done for you by the factory.
        public void ConfigureContainer(ContainerBuilder builder)
        {
            autofacKernel = new AutofacKernel(builder);

            var mappingAssemblies = new List<Assembly> { typeof(HeadquartersBoundedContextModule).Assembly };
            var connectionString = Configuration.GetConnectionString("DefaultConnection");
            var unitOfWorkConnectionSettings = new UnitOfWorkConnectionSettings
            {
                ConnectionString = connectionString,
                ReadSideMappingAssemblies = mappingAssemblies,
                PlainStorageSchemaName = "plainstore",
                PlainMappingAssemblies = new List<Assembly>
                {
                    typeof(HeadquartersBoundedContextModule).Assembly,
                    typeof(ProductVersionModule).Assembly,
                },
                PlainStoreUpgradeSettings = new DbUpgradeSettings(typeof(M001_Init).Assembly, typeof(M001_Init).Namespace),
                ReadSideUpgradeSettings = new DbUpgradeSettings(typeof(M001_Init).Assembly, typeof(M001_InitDb).Namespace),
                LogsUpgradeSettings = new DbUpgradeSettings(typeof(M201905171139_AddErrorsTable).Assembly, typeof(M201905171139_AddErrorsTable).Namespace),
                UsersUpgradeSettings = DbUpgradeSettings.FromFirstMigration<M001_AddUsersHqIdentityModel>()
            };

            builder.RegisterAssemblyTypes(typeof(Startup).Assembly)
                .Where(x => x?.Namespace?.Contains("Services.Impl") == true)
                .AsImplementedInterfaces();

            var eventStoreSettings = new PostgreConnectionSettings
            {
                ConnectionString = connectionString,
                SchemaName = "events"
            };

            var eventStoreModule = new PostgresWriteSideModule(eventStoreSettings,
                new DbUpgradeSettings(typeof(M001_AddEventSequenceIndex).Assembly, typeof(M001_AddEventSequenceIndex).Namespace));

            builder.Register<EventBusSettings>((ctx) => new EventBusSettings()); // TODO REMOVE KP-13449

            autofacKernel.Load(
                new NcqrsModule(),
                eventStoreModule,
                new InfrastructureModule(),
                new NLogLoggingModule(),
                new DataCollectionSharedKernelModule(),
                new WebInterviewModule(),
                new DataCollectionSharedKernelModule(),
                new OrmModule(unitOfWorkConnectionSettings),
                new OwinSecurityModule(),
                new FileStorageModule(Configuration["DataStorePath"], false, "bucket", "region", "prefix", "endpoint"),
                new FileInfrastructureModule(),
                GetHqBoundedContextModule(),
                //new CaptchaModule("recaptcha"),
                new ProductVersionModule(typeof(Startup).Assembly)
                );
        }

        private HeadquartersBoundedContextModule GetHqBoundedContextModule()
        {
            string appDataDirectory = Configuration["DataStorePath"];

            var configurationSection = Configuration.GetSection("PreLoading").Get<PreloadingConfig>();
            var sampleImportSettings = new SampleImportSettings(
                 configurationSection.InterviewsImportParallelTasksLimit);

            var trackingSection = Configuration.GetSection("Tracking").Get<TrackingConfig>();

            var userPreloadingSettings =
                new UserPreloadingSettings(
                    configurationSection.MaxAllowedRecordNumber,
                    loginFormatRegex: UserModel.UserNameRegularExpression,
                    emailFormatRegex: configurationSection.EmailFormatRegex,
                    passwordFormatRegex: configurationSection.PasswordStrengthRegularExpression,
                    phoneNumberFormatRegex: configurationSection.PhoneNumberFormatRegex,
                    fullNameMaxLength: UserModel.PersonNameMaxLength,
                    phoneNumberMaxLength: UserModel.PhoneNumberLength,
                    personNameFormatRegex: UserModel.PersonNameRegex);

            var synchronizationSettings = new SyncSettings(origin: Constants.SupervisorSynchronizationOrigin);

            ExternalStoragesSettings externalStoragesSettings = new FakeExternalStoragesSettings();

            if (Configuration.GetSection("ExternalStorages").Exists())
            {
                var externalStoragesSection = Configuration.GetSection("ExternalStorages").Get<ExternalStoragesConfig>();
                externalStoragesSettings = new ExternalStoragesSettings
                {
                    OAuth2 = new ExternalStoragesSettings.OAuth2Settings
                    {
                        RedirectUri = externalStoragesSection.OAuth2.RedirectUri,
                        ResponseType = externalStoragesSection.OAuth2.ResponseType,
                        OneDrive = new ExternalStoragesSettings.ExternalStorageOAuth2Settings
                        {
                            ClientId = externalStoragesSection.OAuth2.OneDrive.ClientId,
                            AuthorizationUri = externalStoragesSection.OAuth2.OneDrive.AuthorizationUri,
                            Scope = externalStoragesSection.OAuth2.OneDrive.Scope
                        },
                        Dropbox = new ExternalStoragesSettings.ExternalStorageOAuth2Settings
                        {
                            ClientId = externalStoragesSection.OAuth2.Dropbox.ClientId,
                            AuthorizationUri = externalStoragesSection.OAuth2.Dropbox.AuthorizationUri,
                            Scope = externalStoragesSection.OAuth2.Dropbox.Scope
                        },
                        GoogleDrive = new ExternalStoragesSettings.ExternalStorageOAuth2Settings
                        {
                            ClientId = externalStoragesSection.OAuth2.GoogleDrive.ClientId,
                            AuthorizationUri = externalStoragesSection.OAuth2.GoogleDrive.AuthorizationUri,
                            Scope = externalStoragesSection.OAuth2.GoogleDrive.Scope
                        },
                    }
                };
            }

            var exportServiceConfig = Configuration.GetSection("Export").Get<ExportServiceConfig>();

            InterviewDataExportSettings exportSettings = new InterviewDataExportSettings(exportServiceConfig.ExportServiceUrl, exportServiceConfig.LimitOfCachedItemsByDenormalizer);

            return new HeadquartersBoundedContextModule(appDataDirectory,
                userPreloadingSettings,
                sampleImportSettings,
                exportSettings,
                synchronizationSettings,
                new TrackingSettings(trackingSection.WebInterviewPauseResumeGraceTimespan),
                externalStoragesSettings: externalStoragesSettings,
                fileSystemEmailServiceSettings: new FileSystemEmailServiceSettings(false, null, null, null, null, null)
            );
        }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddOptions();
            services.AddControllersWithViews()
                .AddNewtonsoftJson();
            services.AddDistributedMemoryCache();
            services.AddSession();
            services.AddResponseCaching();
            services.AddResponseCompression();
            services.AddLogging();
            services.AddSignalR()
                .AddNewtonsoftJsonProtocol();

            services.AddHttpContextAccessor();
            services.AddAutoMapper(typeof(Startup));

            services.AddControllersWithViews();
            services.AddRazorPages();
            services.AddHttpContextAccessor();

            services.AddHqAuthorization();

            services.AddTransient<ICaptchaService, WebCacheBasedCaptchaService>();
            services.AddTransient<ICaptchaProvider, NoCaptchaProvider>();
            services.AddScoped<UnitOfWorkActionFilter>();
            services.AddScoped<InstallationFilter>();

            services.Configure<GzipCompressionProviderOptions>(options =>
            {
                options.Level = CompressionLevel.Optimal;
            });

            services.AddResponseCompression(options =>
            {
                options.EnableForHttps = true;
                options.Providers.Add<GzipCompressionProvider>();
            });

            services.Configure<BrotliCompressionProviderOptions>(options =>
            {
                options.Level = CompressionLevel.Optimal;
            });

            services.AddRequestDecompression(o =>
            {
                o.Providers.Add<GzipDecompressionProvider>();
            });

            services.AddResponseCompression(options =>
            {
                options.EnableForHttps = true;
                options.Providers.Add<BrotliCompressionProvider>();
            });

            services.AddMvc(mvc =>
            {
                mvc.Filters.AddService<UnitOfWorkActionFilter>(1);
                mvc.Filters.AddService<InstallationFilter>(100);
                mvc.Conventions.Add(new OnlyPublicApiConvention());
                var noContentFormatter = mvc.OutputFormatters.OfType<HttpNoContentOutputFormatter>().FirstOrDefault();
                if (noContentFormatter != null)
                {
                    noContentFormatter.TreatNullValueAsNoContent = false;
                }
            })
#if DEBUG
                .AddRazorRuntimeCompilation()
#endif
                ;

            services.AddHqSwaggerGen();

            // configuration
            services.Configure<GoogleMapsConfig>(this.Configuration.GetSection("GoogleMap"));
            services.Configure<PreloadingConfig>(this.Configuration.GetSection("PreLoading"));
            services.Configure<ApkConfig>(this.Configuration.GetSection("Apks"));
            services.Configure<PasswordPolicyConfig>(this.Configuration.GetSection("PasswordPolicy"));
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            InitModules(app, env);

            app.UseStaticFiles();

            app.UseRouting();

            app.UseAuthentication();
            app.UseAuthorization();

            app.UseSwagger();

            app.UseResponseCompression();
            app.UseRequestDecompression();

            app.UseHqSwaggerUI();

            app.UseRequestLocalization(opt =>
            {
                opt.DefaultRequestCulture = new RequestCulture("en-US");
                opt.SupportedCultures = new List<CultureInfo>
                {
                    new CultureInfo("en")
                };
                opt.SupportedUICultures = new List<CultureInfo>
                {
                    new CultureInfo("en"),
                    new CultureInfo("ru"),
                    new CultureInfo("fr"),
                    new CultureInfo("es"),
                    new CultureInfo("ar"),
                    new CultureInfo("zh")
                };
            });

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapDefaultControllerRoute();
                endpoints.MapControllerRoute(
                    name: "default",
                    pattern: "{controller=Reports}/{action=SurveyAndStatuses}/{id?}",
                    defaults: new
                    {
                        controller = "Reports",
                        action = "SurveyAndStatuses"
                    });
                endpoints.MapRazorPages();
            });
        }

        private void InitModules(IApplicationBuilder app, IWebHostEnvironment env)
        {
            var lifetimeScope = app.ApplicationServices.GetAutofacRoot();
            var initTask = autofacKernel.InitCoreAsync(lifetimeScope, false);

            InScopeExecutor.Init(new UnitOfWorkInScopeExecutor(lifetimeScope));

            if (!env.IsDevelopment())
            {
                initTask.Wait();
            }
            else
            {
                initTask.Wait(TimeSpan.FromSeconds(10));
            }
        }
    }
}