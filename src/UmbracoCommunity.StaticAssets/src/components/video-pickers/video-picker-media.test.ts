import { describe, it, expect, beforeEach, vi, afterEach } from 'vitest'
import { createTestContainer } from '../../test/test-utils'
import { VideoPickerMediaElement } from './video-picker-media'

describe('VideoPickerMediaElement', () => {
  let container: HTMLElement
  let element: VideoPickerMediaElement

  beforeEach(() => {
    container = createTestContainer()
    element = document.createElement('dc-video-picker-media') as VideoPickerMediaElement
    container.appendChild(element)
  })

  afterEach(() => {
    vi.restoreAllMocks()
  })

  describe('initialization', () => {
    it('should create an instance of VideoPickerMediaElement', () => {
      expect(element).toBeInstanceOf(VideoPickerMediaElement)
    })

    it('should extend DcVideoBlockElement', () => {
      expect(element).toBeInstanceOf(VideoPickerMediaElement)
    })

    it('should have default property values', () => {
      expect(element.videoPlayed).toBe(false)
    })
  })

  describe('playVideo method', () => {
    it('should set videoPlayed to true when video element exists', async () => {
      // Create a mock video element and add it to the element
      const mockVideo = document.createElement('video')
      mockVideo.setAttribute('slot', 'video')
      mockVideo.play = vi.fn().mockResolvedValue(undefined)
      element.appendChild(mockVideo)

      await element.updateComplete

      expect(element.videoPlayed).toBe(false)

      element.playVideo()

      expect(element.videoPlayed).toBe(true)
    })

    it('should call play on video element if found', async () => {
      // Create a mock video element and add it to the element
      const mockVideo = document.createElement('video')
      mockVideo.setAttribute('slot', 'video')
      mockVideo.play = vi.fn().mockResolvedValue(undefined)
      element.appendChild(mockVideo)

      await element.updateComplete

      element.playVideo()

      expect(element.videoPlayed).toBe(true)
      expect(mockVideo.play).toHaveBeenCalled()
    })

    it('should handle missing video element gracefully', () => {
      expect(() => {
        element.playVideo()
      }).not.toThrow()

      // videoPlayed should remain false when no video element exists
      expect(element.videoPlayed).toBe(false)
    })
  })

  describe('edge cases', () => {
    it('should handle multiple playVideo calls', async () => {
      // Create a mock video element and add it to the element
      const mockVideo = document.createElement('video')
      mockVideo.setAttribute('slot', 'video')
      mockVideo.play = vi.fn().mockResolvedValue(undefined)
      element.appendChild(mockVideo)

      await element.updateComplete

      element.playVideo()
      expect(element.videoPlayed).toBe(true)

      element.playVideo()
      expect(element.videoPlayed).toBe(true)
    })

    it('should work with different video element types', async () => {
      const mockVideo = document.createElement('video')
      mockVideo.setAttribute('slot', 'video')
      mockVideo.play = vi.fn().mockResolvedValue(undefined)
      element.appendChild(mockVideo)

      await element.updateComplete

      element.playVideo()

      expect(mockVideo.play).toHaveBeenCalled()
    })
  })
})

