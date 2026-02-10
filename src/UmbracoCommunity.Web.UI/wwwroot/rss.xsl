<?xml version="1.0" encoding="utf-8"?>
<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
    xmlns:atom="http://www.w3.org/2005/Atom"
    xmlns:dc="http://purl.org/dc/elements/1.1/">
  <xsl:output method="html" encoding="utf-8" indent="yes"/>
  <xsl:template match="/">
    <html lang="en">
      <head>
        <meta charset="utf-8"/>
        <meta name="viewport" content="width=device-width, initial-scale=1"/>
        <title><xsl:value-of select="/rss/channel/title"/> - RSS Feed</title>
        <style>
          *{margin:0;padding:0;box-sizing:border-box}
          body{font-family:-apple-system,BlinkMacSystemFont,"Segoe UI",Roboto,Helvetica,Arial,sans-serif;line-height:1.6;color:#1a1a2e;background:#f8f8fa;max-width:720px;margin:0 auto;padding:2rem 1rem}
          .notice{background:#e8f0fe;border:1px solid #b8d4fe;border-radius:6px;padding:.75rem 1rem;margin-bottom:2rem;font-size:.875rem;color:#1a3a6e}
          .notice a{color:#1a3a6e;font-weight:600}
          h1{font-size:1.5rem;margin-bottom:.25rem}
          .description{color:#555;margin-bottom:2rem}
          .item{border-bottom:1px solid #e0e0e0;padding:1rem 0}
          .item:last-child{border-bottom:none}
          .item h2{font-size:1.1rem;margin-bottom:.25rem}
          .item h2 a{color:#1a1a2e;text-decoration:none}
          .item h2 a:hover{text-decoration:underline}
          .meta{font-size:.8rem;color:#777;margin-bottom:.5rem}
          .meta span+span::before{content:" \00b7 ";color:#ccc}
          .categories{font-size:.8rem;margin-top:.4rem}
          .categories span{background:#eee;border-radius:3px;padding:.1rem .4rem;margin-right:.3rem;display:inline-block;margin-bottom:.2rem}
          .summary{font-size:.9rem;color:#444;margin-top:.4rem}
        </style>
      </head>
      <body>
        <div class="notice">
          This is an RSS feed. Copy the URL into your feed reader to subscribe.
          <a href="https://aboutfeeds.com">Learn more about RSS.</a>
        </div>
        <h1><xsl:value-of select="/rss/channel/title"/></h1>
        <p class="description"><xsl:value-of select="/rss/channel/description"/></p>
        <xsl:for-each select="/rss/channel/item">
          <div class="item">
            <h2><a href="{link}"><xsl:value-of select="title"/></a></h2>
            <div class="meta">
              <span><xsl:value-of select="pubDate"/></span>
              <xsl:if test="dc:creator">
                <span><xsl:value-of select="dc:creator"/></span>
              </xsl:if>
              <xsl:if test="readTime">
                <span><xsl:value-of select="readTime"/></span>
              </xsl:if>
            </div>
            <xsl:if test="category">
              <div class="categories">
                <xsl:for-each select="category">
                  <span><xsl:value-of select="."/></span>
                </xsl:for-each>
              </div>
            </xsl:if>
            <xsl:if test="description">
              <div class="summary"><xsl:value-of select="description"/></div>
            </xsl:if>
          </div>
        </xsl:for-each>
      </body>
    </html>
  </xsl:template>
</xsl:stylesheet>
