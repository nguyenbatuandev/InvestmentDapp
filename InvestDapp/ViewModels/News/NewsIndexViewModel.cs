using System;
using System.Collections.Generic;
using InvestDapp.Shared.Enums;

namespace InvestDapp.ViewModels.News
{
    public class NewsIndexViewModel
    {
        public NewsArticleViewModel? Featured { get; set; }
        public IReadOnlyList<NewsArticleViewModel> Articles { get; set; } = Array.Empty<NewsArticleViewModel>();
        public PaginationViewModel Pagination { get; set; } = new();
        public PostType? FilterType { get; set; }
        public IReadOnlyList<PostType> AvailableTypes { get; set; } = Array.Empty<PostType>();
    }

    public class NewsArticleViewModel
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string CampaignName { get; set; } = string.Empty;
        public string Excerpt { get; set; } = string.Empty;
        public string? ImageUrl { get; set; }
        public DateTime PublishedAt { get; set; }
        public PostType PostType { get; set; }
        public bool IsFeatured { get; set; }
    }

    public class PaginationViewModel
    {
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalItems { get; set; }

        public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling((double)TotalItems / PageSize);
        public bool HasPrevious => Page > 1;
        public bool HasNext => TotalPages > 0 && Page < TotalPages;
    }
}
