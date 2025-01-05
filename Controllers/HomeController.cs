using System.Diagnostics;
using System.Security.Claims;
using GWOTimetable.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;

namespace GWOTimetable.Controllers
{
    [Authorize(Roles = "User")]
    public class HomeController : Controller
    {
        private readonly Db12026Context _context;

        public HomeController()
        {
            _context = new Db12026Context();
        }

        public IActionResult Index()
        {
            return View();
        }
    }
}

