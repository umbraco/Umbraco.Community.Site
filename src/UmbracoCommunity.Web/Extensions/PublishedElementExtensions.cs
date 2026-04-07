using Umbraco.Cms.Core.Models.Blocks;
using Umbraco.Cms.Core.Models.PublishedContent;
using UmbracoCommunity.Web.Models.PublishedModels;
using UmbracoCommunity.Web.Models.ViewModels.Blocks;

namespace UmbracoCommunity.Web.Extensions;

public static class PublishedElementExtensions
{
    public static T As<T>(this IPublishedElement? element) where T : class, IPublishedElement =>
        element as T ?? throw new ArgumentException($"Provided published element is null or not of the expected model type: {typeof(T).FullName}. Element provided is {GetElementDescription(element)}.", nameof(element));

    public static IList<BlockGridRow> ParseBlockGrid(this BlockGridModel? contentBlocks)
    {
        var rows = new List<BlockGridRow>();
        if (contentBlocks != null && contentBlocks.Any())
        {
            var columnSpan = contentBlocks.GridColumns ?? 12;

            var columnCount = 0;
            var blockRow = new BlockGridRow();
            foreach (var block in contentBlocks)
            {
                var newSpan = block.ColumnSpan + columnCount;

                if (newSpan == columnSpan || contentBlocks.Last() == block)
                {
                    blockRow.Blocks.Add(block);
                    blockRow.HasMultipleBlocks = blockRow.Blocks.Count > 1;
                    rows.Add(blockRow);
                    blockRow = new BlockGridRow();

                    columnCount = 0;
                }
                else if (newSpan > columnSpan)
                {
                    // add row to content
                    if (blockRow.Blocks.Any())
                    {
                        blockRow.HasMultipleBlocks = blockRow.Blocks.Count > 1;
                        rows.Add(blockRow);
                    }

                    // create new row
                    blockRow = new BlockGridRow
                    {
                        Blocks = [block]
                    };

                    // set columnCount
                    columnCount = block.ColumnSpan;
                }
                else
                {
                    blockRow.Blocks.Add(block);
                    // update column count
                    columnCount += block.ColumnSpan;
                }
            }
        }
        foreach (var row in rows)
        {
            var firstBlock = row.Blocks.FirstOrDefault();
            if (firstBlock != null && firstBlock.Settings != null && firstBlock.Settings is ISettingsColour colourSettings)
            {
                if (colourSettings.BackgroundColour != null)
                {
                    row.BackgroundColour = colourSettings.BackgroundColour.Color;
                    if (!string.Equals(row.BackgroundColour, "#ffffff", StringComparison.InvariantCultureIgnoreCase))
                    {
                        row.HasBg = true;
                    }
                }
            }
        }
        return rows;
    }

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
