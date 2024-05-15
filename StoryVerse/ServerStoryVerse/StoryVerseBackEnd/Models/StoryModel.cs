using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace StoryVerseBackEnd.Models
{
    public class StoryModel
    {
        [BsonId]
        public ObjectId Id { get; set; }
        [BsonElement("name")]
        public String Name { get; set; }
        [BsonElement("startDate")]
        public DateTime StartDate { get; set; }
        [BsonElement("endDate")]
        public DateTime EndDate { get; set; }
        [BsonElement("dateCreated")]
        public DateTime DateCreated { get; set; }
        [BsonElement("location")]
        public String Location { get; set; }
        [BsonElement("description")]
        public String Description { get; set; }
        [BsonElement("url")]
        public String Url { get; set; }
        [BsonElement("image")]
        public String Image { get; set; }
        [BsonElement("creatorId")]
        public ObjectId CreatorId { get; set; }
    }
}
