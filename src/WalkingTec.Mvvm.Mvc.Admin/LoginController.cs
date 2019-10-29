using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WalkingTec.Mvvm.Core;
using WalkingTec.Mvvm.Mvc;
using WalkingTec.Mvvm.Mvc.Auth;
using WalkingTec.Mvvm.Mvc.Auth.Attribute;

namespace WalkingTec.Mvvm.Admin.Api
{
    [AuthorizeJwt]
    [ApiController]
    [Route("api/[controller]")]
    [ActionDescription("登陆")]
    public class AccountController : BaseApiController
    {
        private readonly ILogger _logger;
        private readonly ITokenRefreshService _tokenRefreshService;
        private readonly IAuthService _authService;
        public AccountController(
            ILogger<AccountController> logger,
            ITokenRefreshService tokenRefreshService,
            IAuthService authService)
        {
            _logger = logger;
            _tokenRefreshService = tokenRefreshService;
            _authService = authService;
        }

        [AllowAnonymous]
        [HttpPost("[action]")]
        public async Task<IActionResult> Login([FromForm]string userid, [FromForm]string password)
        {
            var authService = HttpContext.RequestServices.GetService(typeof(IAuthService)) as IAuthService;
            var user = DC.Set<FrameworkUserBase>()
                            .Include(x => x.UserRoles)
                            .Include(x => x.UserGroups)
                            .Where(x => x.ITCode.ToLower() == userid.ToLower() && x.Password == Utils.GetMD5String(password) && x.IsValid)
                            .SingleOrDefault();

            //如果没有找到则输出错误
            if (user == null)
            {
                return BadRequest("登录失败");
            }
            var roleIDs = user.UserRoles.Select(x => x.RoleId).ToList();
            var groupIDs = user.UserGroups.Select(x => x.GroupId).ToList();
            //查找登录用户的数据权限
            var dpris = DC.Set<DataPrivilege>()
                .Where(x => x.UserId == user.ID || (x.GroupId != null && groupIDs.Contains(x.GroupId.Value)))
                .ToList();
            //生成并返回登录用户信息
            LoginUserInfo rv = new LoginUserInfo();
            rv.Id = user.ID;
            rv.ITCode = user.ITCode;
            rv.Name = user.Name;
            rv.Roles = DC.Set<FrameworkRole>().Where(x => user.UserRoles.Select(y => y.RoleId).Contains(x.ID)).ToList();
            rv.Groups = DC.Set<FrameworkGroup>().Where(x => user.UserGroups.Select(y => y.GroupId).Contains(x.ID)).ToList();
            rv.DataPrivileges = dpris;
            //查找登录用户的页面权限
            var pris = DC.Set<FunctionPrivilege>()
                .Where(x => x.UserId == user.ID || (x.RoleId != null && roleIDs.Contains(x.RoleId.Value)))
                .ToList();
            rv.FunctionPrivileges = pris;
            LoginUserInfo = rv;

            var token = await authService.IssueToken(LoginUserInfo);
            return Content(JsonConvert.SerializeObject(token), "application/json");
        }

        [AllowAnonymous]
        [HttpPost("[action]")]
        [ProducesResponseType(typeof(Token), StatusCodes.Status200OK)]
        public async Task<Token> RefreshToken(string refreshToken)
        {
            return await _tokenRefreshService.RefreshTokenAsync(refreshToken);
        }

        [AllowAnonymous]
        [HttpPost("login1")]
        [ActionDescription("登录")]
        public IActionResult Login1([FromForm] string userid, [FromForm]string password)
        {
            var user = DC.Set<FrameworkUserBase>()
    .Include(x => x.UserRoles).Include(x => x.UserGroups)
    .Where(x => x.ITCode.ToLower() == userid.ToLower() && x.Password == Utils.GetMD5String(password) && x.IsValid)
    .SingleOrDefault();

            //如果没有找到则输出错误
            if (user == null)
            {
                ModelState.AddModelError(" login", "登录失败");
                return BadRequest(ModelState.GetErrorJson());
            }
            var roleIDs = user.UserRoles.Select(x => x.RoleId).ToList();
            var groupIDs = user.UserGroups.Select(x => x.GroupId).ToList();
            //查找登录用户的数据权限
            var dpris = DC.Set<DataPrivilege>()
                .Where(x => x.UserId == user.ID || (x.GroupId != null && groupIDs.Contains(x.GroupId.Value)))
                .ToList();
            //生成并返回登录用户信息
            LoginUserInfo rv = new LoginUserInfo();
            rv.Id = user.ID;
            rv.ITCode = user.ITCode;
            rv.Name = user.Name;
            rv.Roles = DC.Set<FrameworkRole>().Where(x => user.UserRoles.Select(y => y.RoleId).Contains(x.ID)).ToList();
            rv.Groups = DC.Set<FrameworkGroup>().Where(x => user.UserGroups.Select(y => y.GroupId).Contains(x.ID)).ToList();
            rv.DataPrivileges = dpris;
            rv.PhotoId = user.PhotoId;
            //查找登录用户的页面权限
            var pris = DC.Set<FunctionPrivilege>()
                .Where(x => x.UserId == user.ID || (x.RoleId != null && roleIDs.Contains(x.RoleId.Value)))
                .ToList();
            rv.FunctionPrivileges = pris;
            LoginUserInfo = rv;

            LoginUserInfo forapi = new LoginUserInfo();
            forapi.Id = user.ID;
            forapi.ITCode = user.ITCode;
            forapi.Name = user.Name;
            forapi.Roles = rv.Roles;
            forapi.Groups = rv.Groups;
            forapi.PhotoId = rv.PhotoId;
            List<SimpleMenu> ms = new List<SimpleMenu>();

            var menus = DC.Set<FunctionPrivilege>()
                .Where(x => x.UserId == user.ID || (x.RoleId != null && roleIDs.Contains(x.RoleId.Value)))
                .Select(x => x.MenuItem)
                .Where(x => x.MethodName == null)
                .OrderBy(x => x.DisplayOrder)
                .Select(x => new SimpleMenu
                {
                    Id = x.ID.ToString().ToLower(),
                    ParentId = x.ParentId.ToString().ToLower(),
                    Text = x.PageName,
                    Url = x.Url,
                    Icon = x.ICon
                });

            var folders = DC.Set<FrameworkMenu>().Where(x => x.FolderOnly == true).Select(x => new SimpleMenu
            {
                Id = x.ID.ToString().ToLower(),
                ParentId = x.ParentId.ToString().ToLower(),
                Text = x.PageName,
                Url = x.Url,
                Icon = x.ICon
            });
            ms.AddRange(folders);
            foreach (var item in menus)
            {
                if (folders.Any(x => x.Id == item.Id) == false)
                {
                    ms.Add(item);
                }
            }

            List<string> urls = new List<string>();
            urls.AddRange(DC.Set<FunctionPrivilege>()
                .Where(x => x.UserId == user.ID || (x.RoleId != null && roleIDs.Contains(x.RoleId.Value)))
                .Select(x => x.MenuItem)
                .Where(x => x.MethodName != null)
                .Select(x => x.Url)
                );
            urls.AddRange(GlobaInfo.AllModule.Where(x => x.IsApi == true).SelectMany(x => x.Actions).Where(x => (x.IgnorePrivillege == true || x.Module.IgnorePrivillege == true) && x.Url != null).Select(x => x.Url));
            forapi.Attributes = new Dictionary<string, object>();
            forapi.Attributes.Add("Menus", ms);
            forapi.Attributes.Add("Actions", urls);
            return Ok(forapi);
        }

        [AllRights]
        [HttpGet("CheckLogin/{id}")]
        public IActionResult CheckLogin(Guid id)
        {
            if (LoginUserInfo?.Id != id)
            {
                return BadRequest();
            }
            else
            {
                LoginUserInfo forapi = new LoginUserInfo();
                forapi.Id = LoginUserInfo.Id;
                forapi.ITCode = LoginUserInfo.ITCode;
                forapi.Name = LoginUserInfo.Name;
                forapi.Roles = LoginUserInfo.Roles;
                forapi.Groups = LoginUserInfo.Groups;
                forapi.PhotoId = LoginUserInfo.PhotoId;
                List<SimpleMenu> ms = new List<SimpleMenu>();
                var roleIDs = LoginUserInfo.Roles.Select(x => x.ID).ToList();

                var menus = DC.Set<FunctionPrivilege>()
                    .Where(x => x.UserId == LoginUserInfo.Id || (x.RoleId != null && roleIDs.Contains(x.RoleId.Value)))
                    .Select(x => x.MenuItem)
                    .Where(x => x.MethodName == null)
                  .OrderBy(x => x.DisplayOrder)
                  .Select(x => new SimpleMenu
                  {
                      Id = x.ID.ToString().ToLower(),
                      ParentId = x.ParentId.ToString().ToLower(),
                      Text = x.PageName,
                      Url = x.Url,
                      Icon = x.ICon
                  });
                var folders = DC.Set<FrameworkMenu>().Where(x => x.FolderOnly == true).Select(x => new SimpleMenu
                {
                    Id = x.ID.ToString().ToLower(),
                    ParentId = x.ParentId.ToString().ToLower(),
                    Text = x.PageName,
                    Url = x.Url,
                    Icon = x.ICon
                });
                ms.AddRange(folders);
                foreach (var item in menus)
                {
                    if (folders.Any(x => x.Id == item.Id) == false)
                    {
                        ms.Add(item);
                    }
                }
                List<string> urls = new List<string>();
                urls.AddRange(DC.Set<FunctionPrivilege>()
                    .Where(x => x.UserId == LoginUserInfo.Id || (x.RoleId != null && roleIDs.Contains(x.RoleId.Value)))
                    .Select(x => x.MenuItem)
                    .Where(x => x.MethodName != null)
                    .Select(x => x.Url)
                    );
                urls.AddRange(GlobaInfo.AllModule.Where(x => x.IsApi == true).SelectMany(x => x.Actions).Where(x => (x.IgnorePrivillege == true || x.Module.IgnorePrivillege == true) && x.Url != null).Select(x => x.Url));
                forapi.Attributes = new Dictionary<string, object>();
                forapi.Attributes.Add("Menus", ms);
                forapi.Attributes.Add("Actions", urls);
                return Ok(forapi);
            }
        }

        [AllRights]
        [HttpPost("ChangePassword")]
        public IActionResult ChangePassword(ChangePasswordVM vm)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState.GetErrorJson());
            }
            else
            {
                vm.DoChange();
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState.GetErrorJson());
                }
                else
                {
                    return Ok();
                }
            }

        }

        [AllRights]
        [HttpGet("Logout/{id}")]
        public ActionResult Logout(Guid id)
        {
            if (LoginUserInfo?.Id == id)
            {
                LoginUserInfo = null;
                HttpContext.Session.Clear();
            }
            return Ok();
        }

    }

    public class SimpleMenu
    {
        public string Id { get; set; }

        public string ParentId { get; set; }

        public string Text { get; set; }

        public string Url { get; set; }

        public string Icon { get; set; }
    }
}
