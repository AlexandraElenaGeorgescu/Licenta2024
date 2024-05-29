using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace StoryVerseBackEnd.Models
{
    public class MessageModel
    {
        [BsonId]
        public ObjectId Id { get; set; }
        [BsonElement("msg")]
        public string Message { get; set; }
        [BsonElement("dateSent")]
        public DateTime DateSent { get; set; }
        [BsonElement("userId")]
        public ObjectId UserId { get; set; }
        [BsonElement("storyId")]
        public ObjectId StoryId { get; set; }
    }
}
