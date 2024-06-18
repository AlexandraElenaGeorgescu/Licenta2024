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
        public string Name { get; set; }

        [BsonElement("dateCreated")]
        public DateTime DateCreated { get; set; }

        [BsonElement("genre")]
        public string Genre { get; set; }

        [BsonElement("description")]
        public string Description { get; set; }

        [BsonElement("actualStory")]
        public string ActualStory { get; set; }

        [BsonElement("image")]
        public string Image { get; set; }

        [BsonElement("creatorId")]
        public ObjectId CreatorId { get; set; }

        [BsonIgnoreIfNull]
        public double? TextMatchScore { get; set; }
        public string Author { get; set; }
        public string AuthorAvatarUrl { get; set; }
    }
}
