﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace WB.Core.BoundedContexts.Supervisor.Properties {
    using System;
    
    
    /// <summary>
    ///   A strongly-typed resource class, for looking up localized strings, etc.
    /// </summary>
    // This class was auto-generated by the StronglyTypedResourceBuilder
    // class via a tool like ResGen or Visual Studio.
    // To add or remove a member, edit your .ResX file then rerun ResGen
    // with the /str option, or rebuild your VS project.
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("System.Resources.Tools.StronglyTypedResourceBuilder", "15.0.0.0")]
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    public class SupervisorUIResources {
        
        private static global::System.Resources.ResourceManager resourceMan;
        
        private static global::System.Globalization.CultureInfo resourceCulture;
        
        [global::System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        internal SupervisorUIResources() {
        }
        
        /// <summary>
        ///   Returns the cached ResourceManager instance used by this class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        public static global::System.Resources.ResourceManager ResourceManager {
            get {
                if (object.ReferenceEquals(resourceMan, null)) {
                    global::System.Resources.ResourceManager temp = new global::System.Resources.ResourceManager("WB.Core.BoundedContexts.Supervisor.Properties.SupervisorUIResources", typeof(SupervisorUIResources).Assembly);
                    resourceMan = temp;
                }
                return resourceMan;
            }
        }
        
        /// <summary>
        ///   Overrides the current thread's CurrentUICulture property for all
        ///   resource lookups using this strongly typed resource class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        public static global::System.Globalization.CultureInfo Culture {
            get {
                return resourceCulture;
            }
            set {
                resourceCulture = value;
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to No sending devices detected.
        ///Your device ID is &apos;{0}&apos;.
        /// </summary>
        public static string OfflineSync_NoDevicesDetectedFormat {
            get {
                return ResourceManager.GetString("OfflineSync_NoDevicesDetectedFormat", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Receiving interviews.
        /// </summary>
        public static string OfflineSync_ReceivingInterviews {
            get {
                return ResourceManager.GetString("OfflineSync_ReceivingInterviews", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Receiving interviews
        ///from another devices.
        /// </summary>
        public static string OfflineSync_ReceivingInterviewsFromDevices {
            get {
                return ResourceManager.GetString("OfflineSync_ReceivingInterviewsFromDevices", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Interviews by {0}:
        ///{1} of {2} transfered.
        /// </summary>
        public static string OfflineSync_TransferInterviewsFormat {
            get {
                return ResourceManager.GetString("OfflineSync_TransferInterviewsFormat", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Waitnig for action from Interviewer devices.
        /// </summary>
        public static string OfflineSync_WaitingInterviewers {
            get {
                return ResourceManager.GetString("OfflineSync_WaitingInterviewers", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Uploading broken interview packages.
        /// </summary>
        public static string Synchronization_UploadBrokenInterviewPackages {
            get {
                return ResourceManager.GetString("Synchronization_UploadBrokenInterviewPackages", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Uploading interviewer error messages.
        /// </summary>
        public static string Synchronization_UploadExceptions {
            get {
                return ResourceManager.GetString("Synchronization_UploadExceptions", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Uploading interviewers synchronization statistics.
        /// </summary>
        public static string Synchronization_UploadStatistics {
            get {
                return ResourceManager.GetString("Synchronization_UploadStatistics", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Uploaded {0} of {1}.
        /// </summary>
        public static string Synchronization_UploadStatisticsStats {
            get {
                return ResourceManager.GetString("Synchronization_UploadStatisticsStats", resourceCulture);
            }
        }
    }
}