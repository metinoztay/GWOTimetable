using System.Diagnostics;
using System.Security.Claims;
using GWOTimetable.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace GWOTimetable.Controllers
{
    [Authorize(Roles = "User")]
    public class WorkspaceController : Controller
    {
        private readonly Db12026Context _context;

        public WorkspaceController()
        {
            _context = new Db12026Context();
        }


        public async Task<IActionResult> ChangeSelectedWorkspace(Workspace ws)
        {
            var userId = User.FindFirst("UserId")?.Value;
            bool isAnyWorkspace = _context.Workspaces.Any(w => w.WorkspaceId == ws.WorkspaceId && w.UserId.ToString() == userId);
            var workspaces = _context.Workspaces.Where(w => w.UserId.ToString() == userId).OrderBy(w => w.CreatedAt).ToList();

            if (!isAnyWorkspace)
                return RedirectToAction("Logout", "Account");
            else
            {
                var identity = (ClaimsIdentity)User.Identity;
                var existingClaim = identity.FindFirst("WorkspaceId");
                if (existingClaim != null)
                {
                    identity.RemoveClaim(existingClaim);
                }
                identity.AddClaim(new Claim("WorkspaceId", ws.WorkspaceId.ToString()));

                var workspaceNameClaim = identity.FindFirst("WorkspaceName");
                if (workspaceNameClaim != null)
                {
                    identity.RemoveClaim(workspaceNameClaim);
                }
                identity.AddClaim(new Claim("WorkspaceName", workspaces.Find(w => w.WorkspaceId == ws.WorkspaceId).WorkspaceName));

                var newPrincipal = new ClaimsPrincipal(identity);

                await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    newPrincipal,
                    new AuthenticationProperties
                    {
                        IsPersistent = true
                    }
                );

                return RedirectToAction("Dashboard", "Home");
            }
        }

        [HttpGet]
        public IActionResult GetWorkspaceSelectList()
        {
            var userId = User.FindFirst("UserId")?.Value;
            var workspaces = _context.Workspaces.Where(w => w.UserId.ToString() == userId).OrderBy(w => w.CreatedAt).ToList();

            return PartialView("_WorkspaceSelectListPartial", workspaces);
        }

    }
}

