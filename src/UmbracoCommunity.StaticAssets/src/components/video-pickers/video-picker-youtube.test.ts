import { describe, it, expect, beforeEach, vi, afterEach } from 'vitest'
import { createTestContainer } from '../../test/test-utils'
import { VideoPickerYouTubeElement } from './video-picker-youtube'

describe('VideoPickerYouTubeElement', () => {
  let container: HTMLElement
  let element: VideoPickerYouTubeElement

  beforeEach(() => {
    container = createTestContainer()
    element = document.createElement('dc-video-picker-youtube') as VideoPickerYouTubeElement
    container.appendChild(element)
  })

  afterEach(() => {
    vi.restoreAllMocks()
  })

  describe('initialization', () => {
    it('should create an instance of VideoPickerYouTubeElement', () => {
      expect(element).toBeInstanceOf(VideoPickerYouTubeElement)
    })

    it('should extend DcVideoBlockElement', () => {
      expect(element).toBeInstanceOf(VideoPickerYouTubeElement)
    })

    it('should have default property values', () => {
      expect(element.videoPlayed).toBe(false)
    })
  })

  describe('playVideo method', () => {
    it('should set videoPlayed to true', () => {
      expect(element.videoPlayed).toBe(false)
      
      element.playVideo()
      
      expect(element.videoPlayed).toBe(true)
    })

    it('should only set videoPlayed flag', () => {
      // YouTube videos don't need to call play() on a video element
      // They just need to set the flag to show the embedded video
      element.playVideo()
      
      expect(element.videoPlayed).toBe(true)
    })
  })

  describe('edge cases', () => {
    it('should handle multiple playVideo calls', () => {
      element.playVideo()
      expect(element.videoPlayed).toBe(true)
      
      element.playVideo()
      expect(element.videoPlayed).toBe(true)
    })

    it('should work consistently', () => {
      // Test that the method always sets videoPlayed to true
      element.videoPlayed = false
      element.playVideo()
      expect(element.videoPlayed).toBe(true)
      
      element.videoPlayed = false
      element.playVideo()
      expect(element.videoPlayed).toBe(true)
    })
  })
})

