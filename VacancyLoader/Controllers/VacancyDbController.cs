using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VacancyLoader.Models;

namespace VacancyLoader.Controllers
{
    public class VacancyDbController : Controller
    {
        private readonly VacancyDbContext _context;


        public VacancyDbController(VacancyDbContext context)
        {
            _context = context;
        }
    }



}
