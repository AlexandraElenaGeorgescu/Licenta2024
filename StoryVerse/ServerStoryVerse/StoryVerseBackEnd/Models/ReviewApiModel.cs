using MongoDB.Bson;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace StoryVerseBackEnd.Models
{
    public class ReviewApiModel
    {
        public int Rating { get; set; }
        public String Opinion { get; set; }
        public String LastEdit { get; set; }
        public String UserName { get; set; }
    }
}
