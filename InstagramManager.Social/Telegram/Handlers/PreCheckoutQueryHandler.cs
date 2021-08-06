using MongoDB.Driver;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot.Types.Payments;

namespace InstagramManager.Social.Telegram.Handlers
{
    public partial class UpdateHandler
    {
        private async Task HandlePreCheckoutQuery(PreCheckoutQuery q, CancellationToken ct)
        {
            if (!await _context.Products.Find(p => p.Id == q.InvoicePayload && p.IsActive).AnyAsync(ct))
            {
                try
                {
                    await _bot.AnswerPreCheckoutQueryAsync(q.Id, _config["text_product_unavailable"], ct);
                }
                catch
                {
                    // silent
                }

                return;
            }

            await _bot.AnswerPreCheckoutQueryAsync(q.Id, ct);
        }
    }
}
