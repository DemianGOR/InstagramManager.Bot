using InstagramApiSharp.API.Builder;
using InstagramApiSharp.Classes;
using InstagramApiSharp.Logger;
using InstagramManager.Data.Context;
using InstagramManager.Data.Models;
using InstagramManager.Social.Instagram;
using InstagramManager.Social.Instagram.State;
using InstagramManager.Social.Telegram;
using InstagramManager.Social.Telegram.Handlers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using Polly;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Extensions.Polling;

namespace InstagramManager.Bot
{
    internal static class Program
    {
        private static async Task Main()
        {
            var host = GetHost();

            {
                var stateManager = host.Services.GetRequiredService<IInstagramStateManager>();
                if (stateManager is IInstagramAdminStateManager adminManager)
                {
                    await adminManager.ProcessAdminAsync(null, default);
                }
            }

            var context = host.Services.GetRequiredService<DataContext>();
            if (await context.Products.CountDocumentsAsync(FilterDefinition<Product>.Empty) == 0)
            {
                await context.Products.InsertManyAsync(new[]
                {
                    new Product
                    {
                        Id = ObjectId.GenerateNewId().ToString(),
                        Title = "VIP 1 день - 50₽",
                        Description = "Vip статус_1 - автоматизация на день 65₽",
                        Items = new List<ProductItem>
                        {
                            new ProductItem { Label = "VIP 1 день", Price = 6500, VipDays = 1 }
                        }
                    },
                    new Product
                    {
                        Id = ObjectId.GenerateNewId().ToString(),
                        Title = "VIP 1 неделя - 300₽",
                        Description = "Vip статус_2 - автоматизация на неделю 300₽",
                        Items = new List<ProductItem>
                        {
                            new ProductItem { Label = "VIP 7 дней", Price = 30000, VipDays = 7 }
                        }
                    },
                    new Product
                    {
                        Id = ObjectId.GenerateNewId().ToString(),
                        Title = "VIP 1 месяц - 1200₽",
                        Description = "Vip статус_3 - автоматизация на месяц 1200₽",
                        Items = new List<ProductItem>
                        {
                            new ProductItem { Label = "VIP 30 дней", Price = 120000, VipDays = 30 }
                        }
                    },
                    new Product
                    {
                        Id = ObjectId.GenerateNewId().ToString(),
                        Title = "700 👑 - 280₽",
                        Description = "700 корон",
                        Items = new List<ProductItem>
                        {
                            new ProductItem { Crowns = 700, Label = "700 👑", Price = 28000 }
                        }
                    },
                    new Product
                    {
                        Id = ObjectId.GenerateNewId().ToString(),
                        Title = "1400 👑 - 500₽",
                        Description = "1400 корон",
                        Items = new List<ProductItem>
                        {
                            new ProductItem { Crowns = 1400, Label = "1400 👑", Price = 50000 }
                        }
                    },
                    new Product
                    {
                        Id = ObjectId.GenerateNewId().ToString(),
                        Title = "2800 👑 - 1000₽",
                        Description = "2800 корон",
                        Items = new List<ProductItem>
                        {
                            new ProductItem { Crowns = 2800, Label = "2800 👑", Price = 100000 }
                        }
                    }
                });
            }

            await host.RunAsync();
        }

        private static IHost GetHost() =>
            new HostBuilder()
                .UseEnvironment(Environments.Development)
                .ConfigureHostConfiguration(builder => builder.AddEnvironmentVariables())
                .ConfigureAppConfiguration((context, builder) =>
                {
                    builder.AddJsonFile("appsettings.json", false, true)
                        .AddJsonFile($"appsettings.{context.HostingEnvironment.EnvironmentName}.json", true, true);

                    if (context.HostingEnvironment.IsDevelopment())
                    {
                        builder.AddJsonFile("texts.json", false, true);
                    }
                })
                .ConfigureServices((context, services) =>
                {
                    services.Configure<DataOptions>(context.Configuration.GetSection(nameof(DataOptions)));
                    services.AddSingleton<DataContext>();

                    services.AddSingleton(RequestDelay.FromSeconds(1, 3));

                    services.AddSingleton<IInstaApiHttpContainer, InstaApiHttpContainer>();
                    services.AddHttpClient<IInstaApiHttpContainer, InstaApiHttpContainer>()
                        .ConfigureHttpClient(client =>
                        {
                            client.BaseAddress = new Uri("https://i.instagram.com");
                        })
                        .AddTransientHttpErrorPolicy(policy =>
                            policy.WaitAndRetryAsync(Enumerable.Repeat(TimeSpan.FromMilliseconds(200d), 3)));

                    services.AddSingleton(provider =>
                    {
                        var httpContainer = provider.GetRequiredService<IInstaApiHttpContainer>();
                        return InstaApiBuilder.CreateBuilder()
                            .UseHttpClient(httpContainer.Instance)
                            .SetRequestDelay(RequestDelay.FromSeconds(1, 3))
                            .UseLogger(new DebugLogger(InstagramApiSharp.Logger.LogLevel.Exceptions))
                            .Build();
                    });
                    services.Configure<InstagramOptions>(context.Configuration.GetSection(nameof(InstagramOptions)));
                    services.AddSingleton<IInstagramStateManager, InstagramStateManager>();

                    services.AddSingleton<ITelegramBotHttpContainer, TelegramBotHttpContainer>();
                    services.AddHttpClient<ITelegramBotHttpContainer, TelegramBotHttpContainer>()
                        .AddTransientHttpErrorPolicy(policy =>
                            policy.WaitAndRetryAsync(Enumerable.Repeat(TimeSpan.FromMilliseconds(200d), 3)));

                    services.AddSingleton<ITelegramBotClient, TelegramBotClient>(provider =>
                    {
                        var container = provider.GetRequiredService<ITelegramBotHttpContainer>();
                        return new TelegramBotClient(context.Configuration["bot_token"], container.Instance);
                    });
                    services.AddSingleton<IUpdateHandler, UpdateHandler>();
                    services.AddHostedService<TelegramService>();
                })
                .ConfigureLogging((context, builder) =>
                {
                    builder.AddConfiguration(context.Configuration);
                    builder.AddDebug();
                    builder.AddConsole();
                })
                .UseSystemd()
                .Build();
    }
}