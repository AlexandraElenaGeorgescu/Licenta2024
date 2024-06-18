using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Bson;
using Swashbuckle.AspNetCore.Swagger;
using StoryVerseBackEnd.Models;
using StoryVerseBackEnd.Utils;

namespace StoryVerseBackEnd
{
    public class Startup
    {
        private readonly ILogger<Startup> _logger;
        public Startup(IConfiguration configuration, ILogger<Startup> logger)
        {
            Configuration = configuration;
            _logger = logger;
        }

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddCors(options =>
            {
                options.AddPolicy("AllowSpecificOrigin",
                    builder => builder.WithOrigins("http://localhost:4200", "http://127.0.0.1:4200")
                                      .AllowAnyMethod()
                                      .AllowAnyHeader());
            });

            services.AddMvc().SetCompatibilityVersion(CompatibilityVersion.Version_2_1);

            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new Info { Title = "StoryVerse", Version = "v1" });
            });

            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ClockSkew = TimeSpan.Zero,
                    ValidIssuer = "StoryVerse Server",
                    ValidAudience = "StoryVerse Client",
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Environment.GetEnvironmentVariable("JwtKey")))
                };
            });

            JwtUtil.setSecurityKey(Environment.GetEnvironmentVariable("JwtKey"));

            MongoUtil.InitializeConnection(Environment.GetEnvironmentVariable("MongoDBConnectionString"),
                                            Environment.GetEnvironmentVariable("MongoDBDatabaseName"));
        }


        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseHsts();
            }

            app.UseCors(builder => builder.AllowAnyHeader()
                .AllowAnyMethod()
                .AllowAnyOrigin()
                .AllowCredentials());
            app.UseCors("AllowSpecificOrigin");

            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "StoryVerse V1");
            });

            var websockets = new WebSocketOptions
            {
                KeepAliveInterval = TimeSpan.FromSeconds(2000),
                ReceiveBufferSize = 600 * 1024
            };

            app.UseWebSockets(websockets);
            app.Use(async (context, next) => {
                if (context.Request.Path == "/ws")
                {
                    if (context.WebSockets.IsWebSocketRequest)
                    {
                        var socket = await context.WebSockets.AcceptWebSocketAsync();
                        await PingRequest(context, socket);
                    }
                    else
                    {
                        context.Response.StatusCode = 400;
                    }
                }
                else
                {
                    await next();
                }
            });

            app.UseHttpsRedirection();
            app.UseAuthentication();
            app.UseStaticFiles();
            app.UseStaticFiles(new StaticFileOptions()
            {
                FileProvider = new PhysicalFileProvider(Path.Combine(Directory.GetCurrentDirectory(), @"StaticFiles")),
                RequestPath = new PathString("/StaticFiles")
            });
            app.UseMvc();
        }

        private ConcurrentDictionary<string, ConcurrentBag<WebSocket>> chatRooms = new ConcurrentDictionary<string, ConcurrentBag<WebSocket>>();

        private async Task PingRequest(HttpContext context, WebSocket socket)
        {
            var buffer = new byte[600 * 1024];
            WebSocketReceiveResult result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

            string received = Encoding.Default.GetString(buffer, 0, result.Count);
            string[] splits = received.Split(' ');

            if (splits.Length < 2)
            {
                _logger.LogError("Invalid WebSocket message received.");
                await socket.CloseAsync(WebSocketCloseStatus.InvalidPayloadData, "Invalid message format", CancellationToken.None);
                return;
            }

            string storyIdStr = splits[0];
            ObjectId storyId = new ObjectId(storyIdStr);
            ObjectId userId = new ObjectId(JwtUtil.GetUserIdFromToken("Bearer " + splits[1]));
            string userName = MongoUtil.GetUser(userId).Name;

            if (chatRooms.ContainsKey(storyIdStr))
                chatRooms[storyIdStr].Add(socket);
            else
                chatRooms.TryAdd(storyIdStr, new ConcurrentBag<WebSocket> { socket });

            while (!result.CloseStatus.HasValue)
            {
                result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                received = Encoding.Default.GetString(buffer, 0, result.Count);

                if (string.IsNullOrEmpty(received))
                    continue;

                var msg = new MessageModel
                {
                    Message = received,
                    UserId = userId,
                    StoryId = storyId,
                    DateSent = DateTime.Now
                };

                MongoUtil.SaveMessage(msg);
                var msgApi = msg.getMessageApiModel(userName);
                string toSend = msgApi.Message + "!#|||#!" + msgApi.DateSent + "!#|||#!" + msgApi.UserName;
                byte[] sendBuffer = Encoding.ASCII.GetBytes(toSend);

                foreach (WebSocket ws in chatRooms[storyIdStr])
                {
                    if (ws != socket && ws.State == WebSocketState.Open)
                    {
                        await ws.SendAsync(new ArraySegment<byte>(sendBuffer, 0, toSend.Length), result.MessageType, result.EndOfMessage, CancellationToken.None);
                    }
                }
            }

            await socket.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription, CancellationToken.None);
        }
    }
}
