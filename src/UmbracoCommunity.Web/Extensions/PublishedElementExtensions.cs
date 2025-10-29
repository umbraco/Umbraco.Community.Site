using Umbraco.Cms.Core.Models.Blocks;
using Umbraco.Cms.Core.Models.PublishedContent;

namespace UmbracoCommunity.Web.Extensions;

public static class PublishedElementExtensions
{
    public static T As<T>(this IPublishedElement? element) where T : class, IPublishedElement =>
        element as T ?? throw new ArgumentException($"Provided published element is null or not of the expected model type: {typeof(T).FullName}. Element provided is {GetElementDescription(element)}.", nameof(element));

    public static List<List<BlockGridItem>> GetRows(this BlockGridModel items)
    {
        var totalColumns = items.GridColumns ?? 12;

        var rows = new List<List<BlockGridItem>>();
        var columnCount = 0;
        var blockRow = new List<BlockGridItem>();
        foreach (var block in items)
        {
            var newSpan = block.ColumnSpan + columnCount;

            if (newSpan == totalColumns || items.Last() == block)
            {
                blockRow.Add(block);
                rows.Add(blockRow);

                blockRow = new List<BlockGridItem>();
                columnCount = 0;
            }
            else if (newSpan > totalColumns)
            {
                // add row to content
                if (blockRow.Any())
                {
                    rows.Add(blockRow);
                }

                // create new row
                blockRow = [block];
                ;

                // set columnCount
                columnCount = block.ColumnSpan;
            }
            else
            {
                blockRow.Add(block);
                // update column count
                columnCount += block.ColumnSpan;
            }
        }

        return rows;
    }
    private static string GetElementDescription(IPublishedElement? element)
    {
        if (element is null)
        {
            return "null";
        }

        return $" of content type alias: {element.ContentType.Alias}";
    }
}
