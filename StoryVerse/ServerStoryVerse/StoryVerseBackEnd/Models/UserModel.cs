using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace StoryVerseBackEnd.Models
{
    public class UserModel
    {
        [BsonId]
        public ObjectId Id { get; set; }
        [BsonElement("email")]
        public String Email { get; set; }
        [BsonElement("password")]
        public String Password { get; set; }
        [BsonElement("birthday")]
        public DateTime Birthday { get; set; }
        [BsonElement("name")]
        public String Name { get; set; }
        [BsonElement("surname")]
        public String Surname { get; set; }
        [BsonElement("registeredStories")]
        public List<ObjectId> RegisteredStories { get; set; }
    }
}
