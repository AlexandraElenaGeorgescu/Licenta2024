using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace StoryVerseBackEnd.Models
{
    public class StoryApiModel
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string DateCreated { get; set; }
        public string Genre { get; set; }
        public string Description { get; set; }
        public string ActualStory { get; set; }
        public string Image { get; set; }
    }
}
