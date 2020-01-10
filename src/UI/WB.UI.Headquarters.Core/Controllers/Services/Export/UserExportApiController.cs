﻿using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using AngleSharp.Network.Default;
using Main.Core.Entities.SubEntities;
using Microsoft.AspNetCore.Mvc;
using WB.Core.BoundedContexts.Headquarters.Users;
using WB.Core.BoundedContexts.Headquarters.Views.User;

namespace WB.UI.Headquarters.Controllers.Services.Export
{
    public class UserDto
    {
        public Guid Id { get; set; }
        public string UserName { get; set; }
        public UserRoles[] Roles { get; set; }
    }

    [Route("api/export/v1")]
    public class UserExportApiController : Controller
    {
        private readonly IUserRepository userRepository;

        public UserExportApiController(IUserRepository userRepository)
        {
            this.userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        }

        [Route("user/{id}")]
        [ServiceApiKeyAuthorization]
        [HttpGet]
        public ActionResult<UserDto> Get(string id)
        {
            var userId = Guid.Parse(id);
            var userModel = this.userRepository.Users
                .SingleOrDefault(user => user.Id == userId);

            if (userModel == null) return NotFound($"User with id {id} not found");

            return new UserDto {Id = userModel.Id, UserName = userModel.UserName, Roles = userModel.Roles.Select(r => r.Id.ToUserRole()).ToArray()};
        }
    }
}