import { describe, it, expect } from 'vitest'
import { prepareHeadingHtml } from './prepare-heading-html'

describe('prepareHeadingHtml', () => {
  describe('paragraph tag removal', () => {
    it('should remove opening paragraph tags', () => {
      const html = '<p>Hello World'
      const result = prepareHeadingHtml(html)
      expect(result).toBe('Hello World')
    })

    it('should remove closing paragraph tags', () => {
      const html = 'Hello World</p>'
      const result = prepareHeadingHtml(html)
      expect(result).toBe('Hello World')
    })

    it('should remove both opening and closing paragraph tags', () => {
      const html = '<p>Hello World</p>'
      const result = prepareHeadingHtml(html)
      expect(result).toBe('Hello World')
    })

    it('should remove multiple paragraph tags', () => {
      const html = '<p>First paragraph</p><p>Second paragraph</p>'
      const result = prepareHeadingHtml(html)
      expect(result).toBe('First paragraphSecond paragraph')
    })

    it('should remove nested paragraph tags', () => {
      const html = '<p><p>Nested content</p></p>'
      const result = prepareHeadingHtml(html)
      expect(result).toBe('Nested content')
    })
  })

  describe('preserving other HTML', () => {
    it('should preserve other HTML tags', () => {
      const html = '<p><strong>Bold text</strong></p>'
      const result = prepareHeadingHtml(html)
      expect(result).toBe('<strong>Bold text</strong>')
    })

    it('should preserve multiple different tags', () => {
      const html = '<p><em>Italic</em> and <span>span</span></p>'
      const result = prepareHeadingHtml(html)
      expect(result).toBe('<em>Italic</em> and <span>span</span>')
    })

    it('should preserve links', () => {
      const html = '<p><a href="https://example.com">Link text</a></p>'
      const result = prepareHeadingHtml(html)
      expect(result).toBe('<a href="https://example.com">Link text</a>')
    })

    it('should preserve div tags', () => {
      const html = '<div><p>Content</p></div>'
      const result = prepareHeadingHtml(html)
      expect(result).toBe('<div>Content</div>')
    })

    it('should preserve heading tags', () => {
      const html = '<p><h1>Heading</h1></p>'
      const result = prepareHeadingHtml(html)
      expect(result).toBe('<h1>Heading</h1>')
    })
  })

  describe('edge cases', () => {
    it('should handle undefined input', () => {
      const result = prepareHeadingHtml(undefined as any)
      expect(result).toBeUndefined()
    })

    it('should handle null input', () => {
      const result = prepareHeadingHtml(null as any)
      expect(result).toBeNull()
    })

    it('should handle empty string', () => {
      const result = prepareHeadingHtml('')
      expect(result).toBe('')
    })

    it('should handle whitespace-only string', () => {
      const result = prepareHeadingHtml('   ')
      expect(result).toBe('   ')
    })

    it('should handle string without paragraph tags', () => {
      const html = 'Just plain text'
      const result = prepareHeadingHtml(html)
      expect(result).toBe('Just plain text')
    })

    it('should handle HTML with only other tags', () => {
      const html = '<div><span>No paragraphs here</span></div>'
      const result = prepareHeadingHtml(html)
      expect(result).toBe('<div><span>No paragraphs here</span></div>')
    })
  })

  describe('paragraph tag variants', () => {
    it('should handle paragraph tags with attributes', () => {
      const html = '<p class="test">Content</p>'
      const result = prepareHeadingHtml(html)
      // Current implementation only removes plain <p> and </p> tags, so closing </p> is removed
      expect(result).toBe('<p class="test">Content')
    })

    it('should handle paragraph tags with multiple attributes', () => {
      const html = '<p id="test" class="paragraph" style="color: red;">Content</p>'
      const result = prepareHeadingHtml(html)
      // Current implementation only removes plain <p> and </p> tags, so closing </p> is removed
      expect(result).toBe('<p id="test" class="paragraph" style="color: red;">Content')
    })

    it('should be case sensitive for paragraph tags', () => {
      const html = '<P>Upper case P</P>'
      const result = prepareHeadingHtml(html)
      // Current implementation is case sensitive
      expect(result).toBe('<P>Upper case P</P>')
    })
  })

  describe('multiline content', () => {
    it('should handle multiline paragraph content', () => {
      const html = `<p>Line 1
Line 2
Line 3</p>`
      const result = prepareHeadingHtml(html)
      expect(result).toBe(`Line 1
Line 2
Line 3`)
    })

    it('should handle multiple paragraphs across lines', () => {
      const html = `<p>First paragraph</p>
<p>Second paragraph</p>`
      const result = prepareHeadingHtml(html)
      expect(result).toBe(`First paragraph
Second paragraph`)
    })
  })

  describe('special characters', () => {
    it('should handle HTML entities', () => {
      const html = '<p>&amp; &lt; &gt; &quot;</p>'
      const result = prepareHeadingHtml(html)
      expect(result).toBe('&amp; &lt; &gt; &quot;')
    })

    it('should handle unicode characters', () => {
      const html = '<p>Hello 🌍 World € ñ</p>'
      const result = prepareHeadingHtml(html)
      expect(result).toBe('Hello 🌍 World € ñ')
    })

    it('should handle mixed content with special characters', () => {
      const html = '<p><strong>Bold</strong> &amp; <em>italic</em></p>'
      const result = prepareHeadingHtml(html)
      expect(result).toBe('<strong>Bold</strong> &amp; <em>italic</em>')
    })
  })
})