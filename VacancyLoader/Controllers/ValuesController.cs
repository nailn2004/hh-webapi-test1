using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using VacancyLoader.Models;
using System.Net;
using System.IO;
using AngleSharp.Dom.Html;
using AngleSharp.Parser.Html;
using AngleSharp.Dom;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;

namespace VacancyLoader.Controllers
{
    [Route("api/[controller]")]
    public class ValuesController : Controller
    {
        public IConfigurationRoot Configuration { get; }
        private readonly VacancyDbContext _vacancyDbContext;

        public ValuesController(IHostingEnvironment env)
        {            
            var builder = new ConfigurationBuilder()
               .SetBasePath(env.ContentRootPath)
               .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
               .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true)
               .AddEnvironmentVariables();
            Configuration = builder.Build();
            
            var optionsBuilder = new DbContextOptionsBuilder<VacancyDbContext>();
            var options = optionsBuilder
                    .UseSqlServer(Configuration.GetConnectionString("VacancyDbContext"))
                    .Options;
            _vacancyDbContext = new VacancyDbContext(options);
        }


        // GET api/values
        [HttpGet]
        public IEnumerable<Vacancy> Get()
        {
            List<Vacancy> vacancies;
            try
            {
                vacancies = GetVacancies();
            }
            catch
            {
                vacancies = null;
            }
            if (vacancies != null)
            {
                // Использование транзакции, иначе при удалении и вставке данных создаются разные транзакции
                using (var transaction = _vacancyDbContext.Database.BeginTransaction())
                {
                    try
                    {
                        _vacancyDbContext.Vacancy.RemoveRange(_vacancyDbContext.Vacancy);
                        _vacancyDbContext.SaveChanges();
                        _vacancyDbContext.Vacancy.AddRange(vacancies);
                        _vacancyDbContext.SaveChanges();
                        transaction.Commit();
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }                
            }
            else
            {
                vacancies = _vacancyDbContext.Vacancy.Take(50).ToList();
            }
            return vacancies;

            /*}
            catch
            {
                Response.StatusCode = (int)HttpStatusCode.BadRequest;
                return null;
            }*/
            
        }

        // GET api/values/5
        [HttpGet("{id}")]
        public Vacancy Get(int id)
        {
            Vacancy vacancy;
            try
            {
                vacancy = new Vacancy(id);
                FillVacancyFields(vacancy);
            }
            catch
            {
                vacancy = null;
            }
            if (vacancy != null)
            {
                // Использование транзакции, иначе при удалении и вставке данных создаются разные транзакции
                using (var transaction = _vacancyDbContext.Database.BeginTransaction())
                {
                    try
                    {
                        _vacancyDbContext.Vacancy.Remove(vacancy);
                        _vacancyDbContext.SaveChanges();
                        _vacancyDbContext.Vacancy.Add(vacancy);
                        _vacancyDbContext.SaveChanges();
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
            else
            {
                vacancy = _vacancyDbContext.Vacancy.FirstOrDefault(v => v.VacancyId == id);
            }
            return vacancy;
            /*}
            catch
            {
                Response.StatusCode = (int)HttpStatusCode.BadRequest;
                return null;
            }*/
        }

        
        /* 
        // POST api/values
        [HttpPost]
        public void Post([FromBody]string value)
        {
            throw new NotSupportedException(); 
        }

        // PUT api/values/5
        [HttpPut("{id}")]
        public void Put(int id, [FromBody]string value)
        {
            throw new NotSupportedException();
        }

        // DELETE api/values/5
        [HttpDelete("{id}")]
        public void Delete(int id)
        {
            throw new NotSupportedException();
        }
        */

        // Загрузка текста html по заданному адресу
        [NonAction]
        protected string GetHtmlText(string url)
        {            
            var request = HttpWebRequest.Create(url);
            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            {
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    using (Stream stream = response.GetResponseStream())
                    {
                        using (StreamReader reader = new StreamReader(stream))
                        {
                            return reader.ReadToEnd();
                        }
                    }
                }
                else
                {
                    throw new Exception("not response for url: " + url);
                }
            }
        }


        // Получение списка вакансий
        [NonAction]
        protected List<Vacancy> GetVacancies()
        {
            string html = GetHtmlText(Configuration["UrlOfList"]);

            List<Vacancy> VacanciesList = new List<Vacancy>();
            IHtmlDocument angle = new HtmlParser().Parse(html);
            int i = 0;
            foreach (IElement vacancyElement in angle.QuerySelector("div.vacancy-serp").Children)
            {
                if (i++ == 0) continue; // первый элемент не отображается на странице в браузере, его формат нестандартный
                                
                int vacancyId;
                
                // Получение заголовка для нахождения Id
                var headerElement = vacancyElement.QuerySelectorAll("a")
                        .Where(elem => elem.GetAttribute("data-qa") == "vacancy-serp__vacancy-title")
                        .FirstOrDefault();
                // Получение Id
                if (headerElement != null) {                     
                    string href = headerElement.GetAttribute("href");
                    vacancyId = int.Parse(href.Substring(href.LastIndexOf('/') + 1));
                    Vacancy vacancy = new Vacancy(vacancyId);
                    VacanciesList.Add(vacancy);
                }
                else
                    throw new Exception("error: 'VacancyId' not found");
            }

            // Загрузка информации для каждого объекта по имеющимся Id, загруженным из списка
            Parallel.ForEach<Vacancy>(VacanciesList, new ParallelOptions { MaxDegreeOfParallelism = 10 }, FillVacancyFields);

            return VacanciesList;
        }

        // Заполнение полей объекта Vacancy по имеющемуся VacancyId
        [NonAction]
        protected void FillVacancyFields(Vacancy vacancy)
        {
            string html = GetHtmlText(Configuration["UrlOfItem"] + vacancy.VacancyId.ToString());
            IHtmlDocument angle = new HtmlParser().Parse(html);

            // Получение заголовка
            vacancy.Header = angle.QuerySelector("h1.header").TextContent.Trim();

            // Получение названия организации
            vacancy.Company = angle.QuerySelector("a.vacancy-company-name").TextContent.Trim();

            // Получение оклада
            var salaryElem = angle.QuerySelector("div.vacancy-serp-item__compensation");
            if (salaryElem != null)
                vacancy.Salary = salaryElem.TextContent.Trim();
            else
                vacancy.Salary = angle.QuerySelector("p.vacancy-salary").TextContent.Trim();

            // Получение контактного лица
            var ContactPersons = angle.QuerySelectorAll("p")
                    .Where(elem => elem.GetAttribute("data-qa") == "vacancy-contacts__fio");
            foreach (IElement elem in ContactPersons)
            {
                if (vacancy.ContactPerson == null)
                    vacancy.ContactPerson = ""; 
                else
                    vacancy.ContactPerson += ", ";
                vacancy.ContactPerson += elem.TextContent.Trim();
            }

            // Получение телефона
            var ContactPhones = angle.QuerySelectorAll("p")
                    .Where(elem => elem.GetAttribute("data-qa") == "vacancy-contacts__phone");
            foreach (IElement elem in ContactPhones)
            {
                if (vacancy.Phone == null)
                    vacancy.Phone = ""; 
                else
                    vacancy.Phone += ", ";
                vacancy.Phone += elem.TextContent.Trim();
            }

            // Получение типа занятости
            var EmploymentTypes = angle.QuerySelectorAll("span")
                    .Where(elem => elem.GetAttribute("itemprop") == "employmentType");
            foreach (IElement elem in EmploymentTypes)
            {
                vacancy.EmploymentType = elem.TextContent.Trim();
            }

            // Получение описания (пока теги html остаются как есть 
            // для возможности форматирования при отображении данных)
            var Descriptions = angle.QuerySelectorAll("div")
                    .Where(elem => elem.GetAttribute("data-qa") == "vacancy-description");
            foreach (IElement elem in Descriptions)
            {
                vacancy.Description = elem.InnerHtml;
            }
        }
    }
}
