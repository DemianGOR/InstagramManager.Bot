using InstagramManager.Data.Context;
using InstagramManager.Data.Models;
using MongoDB.Bson;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace InstagramManager.Social.Telegram.GeneratorDeepURL
{
    public class GeneratorDeepURL
    {
        private string Url = "https://t.me/";
        private string NameBot = "manya_test_bot";
        private string StartPromocode = "?start=";
        private string StartInvitedCode = "invite {0}";

        private readonly DataContext _context;
        public GeneratorDeepURL(DataContext context)
        {
            _context = context;
        }
        public async Task<List<string>> GenerateUrlPromocode(int countCode, int Crowns, CancellationToken ct)
        {

            var promoCodes = new string[countCode];
            for(int  b= 0; b < countCode; b++)
            {
                promoCodes[b] = ObjectId.GenerateNewId().ToString();
            }
            // верни там лучше ObjectId
            await _context.Promocodes.InsertManyAsync(promoCodes.Select(p => new Promocodes
            {
                Id = p,
                Cash = Crowns,
                IsUsed = false,
                UsedBy = 0,
            }
            ));
            List<string> url = new List<string>();

            foreach(var s in promoCodes)
            {
               url.Add(Url + NameBot + StartPromocode + s);
            }

            return url;

        }
        public string GenerateUrlInviteCode(int userId)
        {
            return Url + NameBot+ StartPromocode +  Uri.EscapeDataString(string.Format(StartInvitedCode, userId));

        }
    }
}
