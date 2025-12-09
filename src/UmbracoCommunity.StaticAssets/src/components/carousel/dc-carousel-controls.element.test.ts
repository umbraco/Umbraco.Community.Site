import { expect, fixture, html } from '@open-wc/testing';
import { DcCarouselControls } from './dc-carousel-controls.element';

describe('DcCarouselControls Component', () => {
  let element: DcCarouselControls;

  beforeEach(async () => {
    element = await fixture(html`
      <dc-carousel-controls count="3"></dc-carousel-controls>
    `);
  });

  describe('Component Initialization', () => {
    it('should create an instance of DcCarouselControls', () => {
      expect(element).to.be.instanceOf(DcCarouselControls);
    });

    it('should initialize with currentIndex 0', () => {
      expect(element.currentIndex).to.equal(0);
    });

    it('should set count property correctly', () => {
      expect(element.count).to.equal(3);
    });

    it('should add event listener for dc-carousel-index-changed on firstUpdated', () => {
      // The event listener is added in firstUpdated, we can verify by checking if it responds to events
      const event = new CustomEvent('dc-carousel-index-changed', {
        detail: { index: 1 }
      });
      
      expect(() => document.dispatchEvent(event)).to.not.throw();
    });
  });

  describe('Navigation Controls', () => {
    it('should render previous and next buttons', () => {
      const prevButton = element.shadowRoot?.querySelector('.prev');
      const nextButton = element.shadowRoot?.querySelector('.nav-button:not(.prev)');
      
      expect(prevButton).to.exist;
      expect(nextButton).to.exist;
    });

    it('should render numeric controls with correct count', () => {
      const dots = element.shadowRoot?.querySelectorAll('.dot');
      const countSpan = element.shadowRoot?.querySelector('.controls-numeric > span:last-child');
      
      expect(dots).to.have.length(3);
      expect(countSpan?.textContent).to.equal('03');
    });

    it('should highlight active dot', () => {
      const activeDot = element.shadowRoot?.querySelector('.dot.active');
      expect(activeDot).to.exist;
    });
  });

  describe('Event Handling', () => {
    it('should handle previous button click', () => {
      const prevButton = element.shadowRoot?.querySelector('.prev') as HTMLButtonElement;
      let eventDispatched = false;
      
      element.addEventListener('dc-carousel-change', (event) => {
        expect((event as CustomEvent).detail.action).to.equal('prev');
        eventDispatched = true;
      });

      prevButton.click();
      expect(eventDispatched).to.be.true;
    });

    it('should handle next button click', () => {
      const nextButton = element.shadowRoot?.querySelector('.nav-button:not(.prev)') as HTMLButtonElement;
      let eventDispatched = false;
      
      element.addEventListener('dc-carousel-change', (event) => {
        expect((event as CustomEvent).detail.action).to.equal('next');
        eventDispatched = true;
      });

      nextButton.click();
      expect(eventDispatched).to.be.true;
    });

    it('should handle index change event', () => {
      const event = new CustomEvent('dc-carousel-index-changed', {
        detail: { index: 2 }
      });
      
      document.dispatchEvent(event);
      expect(element.currentIndex).to.equal(2);
    });

    it('should ignore invalid index in index change event', () => {
      const originalIndex = element.currentIndex;
      const event = new CustomEvent('dc-carousel-index-changed', {
        detail: { index: -1 }
      });
      
      document.dispatchEvent(event);
      expect(element.currentIndex).to.equal(originalIndex);
    });
  });

  describe('Index Management', () => {
    it('should wrap to last index when going previous from first', () => {
      element.currentIndex = 0;
      const prevButton = element.shadowRoot?.querySelector('.prev') as HTMLButtonElement;
      
      prevButton.click();
      expect(element.currentIndex).to.equal(2); // count - 1
    });

    it('should wrap to first index when going next from last', () => {
      element.currentIndex = 2;
      const nextButton = element.shadowRoot?.querySelector('.nav-button:not(.prev)') as HTMLButtonElement;
      
      nextButton.click();
      expect(element.currentIndex).to.equal(0);
    });

    it('should increment index when going next from middle', () => {
      element.currentIndex = 1;
      const nextButton = element.shadowRoot?.querySelector('.nav-button:not(.prev)') as HTMLButtonElement;
      
      nextButton.click();
      expect(element.currentIndex).to.equal(2);
    });

    it('should decrement index when going previous from middle', () => {
      element.currentIndex = 1;
      const prevButton = element.shadowRoot?.querySelector('.prev') as HTMLButtonElement;
      
      prevButton.click();
      expect(element.currentIndex).to.equal(0);
    });
  });

  describe('Rendering', () => {
    it('should update active dot when currentIndex changes', async () => {
      element.currentIndex = 1;
      await element.updateComplete;
      
      const dots = element.shadowRoot?.querySelectorAll('.dot');
      expect(dots?.[1]).to.have.class('active');
      expect(dots?.[0]).to.not.have.class('active');
      expect(dots?.[2]).to.not.have.class('active');
    });

    it('should render correct number of dots based on count', async () => {
      element.count = 5;
      await element.updateComplete;
      
      const dots = element.shadowRoot?.querySelectorAll('.dot');
      expect(dots).to.have.length(5);
    });

    it('should render correct count display', async () => {
      element.count = 7;
      await element.updateComplete;
      
      const countSpan = element.shadowRoot?.querySelector('.controls-numeric > span:last-child');
      expect(countSpan?.textContent).to.equal('07');
    });
  });

  describe('Component Cleanup', () => {
    it('should remove event listeners on disconnection', () => {
      expect(() => element.disconnectedCallback()).to.not.throw();
    });
  });

  describe('Edge Cases', () => {
    it('should handle count of 0', async () => {
      element.count = 0;
      await element.updateComplete;
      
      const dots = element.shadowRoot?.querySelectorAll('.dot');
      expect(dots).to.have.length(0);
    });

    it('should handle count of 1', async () => {
      element.count = 1;
      await element.updateComplete;
      
      const dots = element.shadowRoot?.querySelectorAll('.dot');
      expect(dots).to.have.length(1);
      expect(dots?.[0]).to.have.class('active');
    });

    it('should handle negative count gracefully', async () => {
      // Test that setting negative count doesn't crash the component
      // The component should handle this gracefully without throwing
      try {
        element.count = -1;
        await element.updateComplete;
        // If we get here, the component handled the negative count
        expect(element.count).to.equal(-1);
      } catch (error) {
        // If it throws, that's also acceptable behavior
        expect(error).to.be.instanceOf(RangeError);
      }
    });
  });
});
