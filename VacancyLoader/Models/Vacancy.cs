using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Threading.Tasks;

namespace VacancyLoader.Models
{
    public class Vacancy
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int VacancyId { get; set; }
        public string Header { get; set; } // заголовок, 
        public string Salary { get; set; } // оклад, 
        public string Company { get; set; } // организация разместившая вакансию, 
        public string ContactPerson { get; set; } // контактное лицо, 
        public string Phone { get; set; } // телефон, 
        public string EmploymentType { get; set; } // тип занятости, 
        public string Description { get; set; } // описание вакансии.        

        public Vacancy()
        {
        }

        public Vacancy(int id) {
            VacancyId = id;
        }
    }
}
