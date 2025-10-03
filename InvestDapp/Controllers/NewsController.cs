using System.Text.RegularExpressions;
using InvestDapp.Infrastructure.Data;
using InvestDapp.Shared.Enums;
using InvestDapp.ViewModels.News;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InvestDapp.Controllers
{
    public class NewsController : Controller
    {
        private readonly InvestDbContext _dbContext;

        public NewsController(InvestDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<IActionResult> Index(PostType? type, int page = 1)
        {
            const int pageSize = 9;
            page = Math.Max(1, page);

            var query = _dbContext.CampaignPosts
                .AsNoTracking()
                .Include(p => p.Campaign)
                .Where(p => p.ApprovalStatus == ApprovalStatus.Approved);

            if (type.HasValue)
            {
                query = query.Where(p => p.PostType == type.Value);
            }

            var totalItems = await query.CountAsync();
            var posts = await query
                .OrderByDescending(p => p.IsFeatured)
                .ThenByDescending(p => p.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var articleModels = posts.Select(p => new NewsArticleViewModel
            {
                Id = p.Id,
                Title = p.Title,
                CampaignName = p.Campaign?.Name ?? "Chiến dịch",
                Excerpt = BuildExcerpt(p.Content, 220),
                ImageUrl = !string.IsNullOrWhiteSpace(p.ImageUrl) ? p.ImageUrl : p.Campaign?.ImageUrl,
                PublishedAt = p.ApprovedAt ?? p.CreatedAt,
                PostType = p.PostType,
                IsFeatured = p.IsFeatured
            }).ToList();

            NewsArticleViewModel? featured = null;
            if (articleModels.Count > 0)
            {
                featured = articleModels.FirstOrDefault(a => a.IsFeatured) ?? articleModels.First();
                articleModels.Remove(featured);
            }

            var model = new NewsIndexViewModel
            {
                Featured = featured,
                Articles = articleModels,
                FilterType = type,
                Pagination = new PaginationViewModel
                {
                    Page = page,
                    PageSize = pageSize,
                    TotalItems = totalItems
                },
                AvailableTypes = Enum.GetValues<PostType>()
            };

            return View(model);
        }

        private static string BuildExcerpt(string? content, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return string.Empty;
            }

            var plain = Regex.Replace(content, "<[^>]+>", string.Empty);
            plain = plain.Replace("\r", string.Empty).Replace("\n", " ").Trim();

            if (plain.Length <= maxLength)
            {
                return plain;
            }

            return plain[..maxLength].TrimEnd() + "…";
        }
    }
}
