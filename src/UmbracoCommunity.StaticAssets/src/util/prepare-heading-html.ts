export function prepareHeadingHtml(html: string): string {
    const tagsToRemove = ['p'];
    let result = html;

    if (!result || result.length === 0)
        return result;

    tagsToRemove.forEach(tag => {
        result = result
            .replace(new RegExp(`<${tag}>`, 'gm'), '')
            .replace(new RegExp(`</${tag}>`, 'gm'), '');
    });

    return result;
}
