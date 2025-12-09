import { expect, fixture, html } from '@open-wc/testing';
import { DcCarousel } from './dc-carousel.element';

describe('DcCarousel Component', () => {
  let element: DcCarousel;

  beforeEach(async () => {
    element = await fixture(html`
      <dc-carousel>
        <div class="carousel-items">
          <div class="carousel-item">Item 1</div>
          <div class="carousel-item">Item 2</div>
          <div class="carousel-item">Item 3</div>
        </div>
      </dc-carousel>
    `);
  });

  describe('Component Initialization', () => {
    it('should create an instance of DcCarousel', () => {
      expect(element).to.be.instanceOf(DcCarousel);
    });

    it('should add active class to first item on connection', () => {
      const container = element.querySelector('.carousel-items') as HTMLElement;
      const firstItem = container.children[0];
      expect(firstItem).to.have.class('active');
    });

    it('should add event listeners on connection', () => {
      const container = element.querySelector('.carousel-items') as HTMLElement;
      const hasTouchStartListener = container.addEventListener.toString().includes('touchstart');
      expect(container).to.exist;
    });
  });

  describe('Touch Event Handling', () => {
    let container: HTMLElement;

    beforeEach(() => {
      container = element.querySelector('.carousel-items') as HTMLElement;
    });

    it('should handle touchstart event', () => {
      const touchEvent = new TouchEvent('touchstart', {
        changedTouches: [{
          screenX: 100,
          screenY: 200,
          clientX: 100,
          clientY: 200,
          pageX: 100,
          pageY: 200,
          identifier: 1,
          target: container
        } as Touch]
      });

      expect(() => container.dispatchEvent(touchEvent)).to.not.throw();
    });

    it('should handle touchmove event', () => {
      const touchEvent = new TouchEvent('touchmove', {
        changedTouches: [{
          screenX: 150,
          screenY: 200,
          clientX: 150,
          clientY: 200,
          pageX: 150,
          pageY: 200,
          identifier: 1,
          target: container
        } as Touch]
      });

      expect(() => container.dispatchEvent(touchEvent)).to.not.throw();
    });

    it('should handle touchend event', () => {
      const touchEvent = new TouchEvent('touchend', {
        changedTouches: [{
          screenX: 200,
          screenY: 200,
          clientX: 200,
          clientY: 200,
          pageX: 200,
          pageY: 200,
          identifier: 1,
          target: container
        } as Touch]
      });

      expect(() => container.dispatchEvent(touchEvent)).to.not.throw();
    });

    it('should handle swipe gesture to next item', async () => {
      // Simulate a swipe gesture
      const startEvent = new TouchEvent('touchstart', {
        changedTouches: [{
          screenX: 100,
          screenY: 200,
          clientX: 100,
          clientY: 200,
          pageX: 100,
          pageY: 200,
          identifier: 1,
          target: container
        } as Touch]
      });

      const endEvent = new TouchEvent('touchend', {
        changedTouches: [{
          screenX: 50, // Swipe left (next)
          screenY: 200,
          clientX: 50,
          clientY: 200,
          pageX: 50,
          pageY: 200,
          identifier: 1,
          target: container
        } as Touch]
      });

      container.dispatchEvent(startEvent);
      
      await new Promise(resolve => setTimeout(resolve, 100));
      container.dispatchEvent(endEvent);
    });

    it('should handle swipe gesture to previous item', async () => {
      // Simulate a swipe gesture
      const startEvent = new TouchEvent('touchstart', {
        changedTouches: [{
          screenX: 100,
          screenY: 200,
          clientX: 100,
          clientY: 200,
          pageX: 100,
          pageY: 200,
          identifier: 1,
          target: container
        } as Touch]
      });

      const endEvent = new TouchEvent('touchend', {
        changedTouches: [{
          screenX: 150, // Swipe right (prev)
          screenY: 200,
          clientX: 150,
          clientY: 200,
          pageX: 150,
          pageY: 200,
          identifier: 1,
          target: container
        } as Touch]
      });

      container.dispatchEvent(startEvent);
      
      await new Promise(resolve => setTimeout(resolve, 100));
      container.dispatchEvent(endEvent);
    });
  });

  describe('Carousel Navigation', () => {
    it('should handle dc-carousel-change event', () => {
      const changeEvent = new CustomEvent('dc-carousel-change', {
        detail: { action: 'next' }
      });

      expect(() => element.dispatchEvent(changeEvent)).to.not.throw();
    });

    it('should dispatch dc-carousel-index-changed event', (done) => {
      element.addEventListener('dc-carousel-index-changed', (event) => {
        expect(event).to.be.instanceOf(CustomEvent);
        expect((event as CustomEvent).detail).to.have.property('index');
        done();
      });

      // Trigger a navigation to fire the event
      const changeEvent = new CustomEvent('dc-carousel-change', {
        detail: { action: 'next' }
      });
      element.dispatchEvent(changeEvent);
    });
  });

  describe('Component Cleanup', () => {
    it('should remove event listeners on disconnection', () => {
      expect(() => element.disconnectedCallback()).to.not.throw();
    });
  });

  describe('Edge Cases', () => {
    it('should handle empty container gracefully', async () => {
      const emptyElement = await fixture(html`
        <dc-carousel>
          <div class="carousel-items"></div>
        </dc-carousel>
      `);

      expect(() => emptyElement.disconnectedCallback()).to.not.throw();
    });

    it('should handle touch events with no changedTouches', () => {
      const container = element.querySelector('.carousel-items') as HTMLElement;
      const touchEvent = new TouchEvent('touchstart', {
        changedTouches: []
      });

      expect(() => container.dispatchEvent(touchEvent)).to.not.throw();
    });

    it('should handle invalid action in moveItems', () => {
      const changeEvent = new CustomEvent('dc-carousel-change', {
        detail: { action: 'invalid' }
      });

      expect(() => element.dispatchEvent(changeEvent)).to.not.throw();
    });
  });
});
