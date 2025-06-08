using Asp.Versioning;

using Blizztrack.API.Filters;
using Blizztrack.Persistence;

using Microsoft.AspNetCore.Mvc;

using NSwag.Annotations;

using System.ComponentModel;

namespace Blizztrack.API
{
    [ApiVersion(1.0)]
    [Tags("Repository endpoints"), OpenApiTag("Repository endpoints", Description = "Endpoints in this category allow the user to enumerate Blizztrack's internal tracking data.")]
    [ApiController, Route("api/v{version:apiVersion}/repository")]
    [TypeFilter<NoCachingFilter>(IsReusable = true)]
    public class DatabaseController(DatabaseContext databaseContext) : ControllerBase
    {
        [HttpGet("products")]
        [OpenApiOperation("Enumerates product codes", "Returns a collection of all currently tracked product codes.")]
        public IQueryable<string> EnumerateProductCodes() => databaseContext.Products.Select(e => e.Code);

        [HttpGet("builds")]
        [OpenApiOperation("Enumerates builds known to Blizztrack", """
            Returns a collection of build information for a specific product code.
            """)]
        public RepositoryResponse<Configuration> EnumerateConfigurations(
            [Description("The product code identifier")] string product,
            [Description("The index of the page to retrieve")] int pageIndex,
            [Description("The amount of entries to retrieve per page")] int pageSize)
        {
            var queryBase = databaseContext.Configs.Where(c => c.Product.Code == product);

            var entryCount = queryBase.Count();
            var pageCount = (int) Math.Ceiling((double) entryCount / pageSize);

            var entries = queryBase.OrderBy(c => c.ID)
                .Skip(pageIndex)
                .Take(pageSize)
                .Select(e => new Configuration(e.ID, e.BuildID, e.Name, e.BuildConfig.AsHexString(), e.CDNConfig.AsHexString(), e.KeyRing.AsHexString(), e.Config.AsHexString(), e.Regions));

            return new(entries, new(pageIndex, pageSize, pageCount));
        }
    }

    public record class Configuration(uint ID, uint BuildID, string Name, string BuildConfig, string CdnConfig, string KeyRing, string Config, string[] Regions);

    public record class RepositoryResponse<T>(IQueryable<T> Entries, PaginationResponseData Pagination);

    public record class PaginationResponseData(int PageIndex, int PageSize, int PageCount);
}