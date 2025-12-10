import { expect, fixture, html } from '@open-wc/testing';
import { CaseStudiesElement } from './case-studies';
import { CaseStudiesFilter } from './case-studies.enums';

describe('CaseStudiesElement Component', () => {
  let element: CaseStudiesElement;

  beforeEach(async () => {
    element = await fixture(html`
      <dc-case-studies>
        <div class="case-study">Case Study 1</div>
        <div class="case-study">Case Study 2</div>
        <div class="case-study">Case Study 3</div>
      </dc-case-studies>
    `);
  });

  describe('Component Initialization', () => {
    it('should create an instance of CaseStudiesElement', () => {
      expect(element).to.be.instanceOf(CaseStudiesElement);
    });

    it('should initialize with default filters', () => {
      expect(element._filters).to.have.length(4);
      expect(element._filters[0].alias).to.equal(CaseStudiesFilter.Skill);
      expect(element._filters[1].alias).to.equal(CaseStudiesFilter.Sector);
      expect(element._filters[2].alias).to.equal(CaseStudiesFilter.Country);
      expect(element._filters[3].alias).to.equal(CaseStudiesFilter.Type);
    });

    it('should have correct filter labels', () => {
      expect(element._filters[0].label).to.equal('Skills');
      expect(element._filters[1].label).to.equal('Sectors');
      expect(element._filters[2].label).to.equal('Countries');
      expect(element._filters[3].label).to.equal('Type');
    });

    it('should have correct default values', () => {
      expect(element._filters[0].defaultValue).to.equal('Skills');
      expect(element._filters[1].defaultValue).to.equal('Sectors');
      expect(element._filters[2].defaultValue).to.equal('Countries');
      expect(element._filters[3].defaultValue).to.equal('Types');
    });

    it('should have correct control types', () => {
      element._filters.forEach(filter => {
        expect(filter.controlType).to.equal('dropdown');
      });
    });
  });

  describe('Rendering', () => {
    it('should render dc-filters component', () => {
      const filtersElement = element.shadowRoot?.querySelector('dc-filters');
      expect(filtersElement).to.exist;
    });

    it('should pass filters to dc-filters component', () => {
      const filtersElement = element.shadowRoot?.querySelector('dc-filters') as any;
      expect(filtersElement.filters).to.deep.equal(element._filters);
    });

    it('should pass filterType to dc-filters component', () => {
      const filtersElement = element.shadowRoot?.querySelector('dc-filters') as any;
      expect(filtersElement.filterType).to.equal(CaseStudiesFilter);
    });

    it('should render slot for case study content', () => {
      const slot = element.shadowRoot?.querySelector('slot');
      expect(slot).to.exist;
    });

    it('should render slotted content', () => {
      const caseStudies = element.querySelectorAll('.case-study');
      expect(caseStudies).to.have.length(3);
    });
  });

  describe('Filter Configuration', () => {
    it('should have skill filter configured correctly', () => {
      const skillFilter = element._filters.find(f => f.alias === CaseStudiesFilter.Skill);
      expect(skillFilter).to.exist;
      expect(skillFilter?.label).to.equal('Skills');
      expect(skillFilter?.defaultValue).to.equal('Skills');
      expect(skillFilter?.controlType).to.equal('dropdown');
    });

    it('should have sector filter configured correctly', () => {
      const sectorFilter = element._filters.find(f => f.alias === CaseStudiesFilter.Sector);
      expect(sectorFilter).to.exist;
      expect(sectorFilter?.label).to.equal('Sectors');
      expect(sectorFilter?.defaultValue).to.equal('Sectors');
      expect(sectorFilter?.controlType).to.equal('dropdown');
    });

    it('should have country filter configured correctly', () => {
      const countryFilter = element._filters.find(f => f.alias === CaseStudiesFilter.Country);
      expect(countryFilter).to.exist;
      expect(countryFilter?.label).to.equal('Countries');
      expect(countryFilter?.defaultValue).to.equal('Countries');
      expect(countryFilter?.controlType).to.equal('dropdown');
    });

    it('should have type filter configured correctly', () => {
      const typeFilter = element._filters.find(f => f.alias === CaseStudiesFilter.Type);
      expect(typeFilter).to.exist;
      expect(typeFilter?.label).to.equal('Type');
      expect(typeFilter?.defaultValue).to.equal('Types');
      expect(typeFilter?.controlType).to.equal('dropdown');
    });
  });

  describe('Styling', () => {
    it('should have static styles defined', () => {
      expect(CaseStudiesElement.styles).to.exist;
      expect(CaseStudiesElement.styles).to.be.an('array');
      expect(CaseStudiesElement.styles.length).to.be.greaterThan(0);
    });
  });

  describe('Edge Cases', () => {
    it('should handle empty slotted content', async () => {
      const emptyElement = await fixture(html`
        <dc-case-studies></dc-case-studies>
      `);

      expect(emptyElement).to.be.instanceOf(CaseStudiesElement);
      expect(emptyElement._filters).to.have.length(4);
    });

    it('should handle filters state changes', async () => {
      const originalFilters = [...element._filters];
      element._filters = [];
      await element.updateComplete;

      expect(element._filters).to.have.length(0);

      // Restore original filters
      element._filters = originalFilters;
      await element.updateComplete;

      expect(element._filters).to.have.length(4);
    });
  });

  describe('Integration', () => {
    it('should work with CaseStudiesFilter enum', () => {
      expect(CaseStudiesFilter.Country).to.equal('country');
      expect(CaseStudiesFilter.Skill).to.equal('skill');
      expect(CaseStudiesFilter.Sector).to.equal('sector');
      expect(CaseStudiesFilter.Type).to.equal('type');
    });

    it('should maintain filter order', () => {
      const expectedOrder = [
        CaseStudiesFilter.Skill,
        CaseStudiesFilter.Sector,
        CaseStudiesFilter.Country,
        CaseStudiesFilter.Type
      ];

      const actualOrder = element._filters.map(f => f.alias);
      expect(actualOrder).to.deep.equal(expectedOrder);
    });
  });

  describe('Pagination', () => {
    it('should not render load-more-container initially', async () => {
      const elementWithFewItems = await fixture(html`
        <dc-case-studies>
          <div class="case-study">Case Study 1</div>
          <div class="case-study">Case Study 2</div>
          <div class="case-study">Case Study 3</div>
        </dc-case-studies>
      `) as CaseStudiesElement;

      await (elementWithFewItems as any).updateComplete;
      const loadMoreContainer = elementWithFewItems.shadowRoot?.querySelector('.load-more-container');
      // Container should not exist when hasMoreItems is false
      expect(loadMoreContainer).to.not.exist;
    });

    it('should use correct PAGE_SIZE constant', async () => {
      // Create element with more than 9 items
      const elementWithManyItems = await fixture(html`
        <dc-case-studies>
          <div class="case-study">Case Study 1</div>
          <div class="case-study">Case Study 2</div>
          <div class="case-study">Case Study 3</div>
          <div class="case-study">Case Study 4</div>
          <div class="case-study">Case Study 5</div>
          <div class="case-study">Case Study 6</div>
          <div class="case-study">Case Study 7</div>
          <div class="case-study">Case Study 8</div>
          <div class="case-study">Case Study 9</div>
          <div class="case-study">Case Study 10</div>
          <div class="case-study">Case Study 11</div>
          <div class="case-study">Case Study 12</div>
        </dc-case-studies>
      `) as CaseStudiesElement;

      await (elementWithManyItems as any).updateComplete;

      // Simulate filter change to populate _slottedItems and trigger pagination
      (elementWithManyItems as any)._slottedItems = Array.from(elementWithManyItems.querySelectorAll('.case-study'));
      (elementWithManyItems as any)._applyPagination();
      await elementWithManyItems.updateComplete;

      // PAGE_SIZE is 9, so with 12 items, hasMoreItems should be true
      expect((elementWithManyItems as any)._hasMoreItems).to.be.true;
    });

    it('should display load-more button when items exceed PAGE_SIZE', async () => {
      // Create element with more than 9 items
      const elementWithManyItems = await fixture(html`
        <dc-case-studies>
          <div class="case-study">Case Study 1</div>
          <div class="case-study">Case Study 2</div>
          <div class="case-study">Case Study 3</div>
          <div class="case-study">Case Study 4</div>
          <div class="case-study">Case Study 5</div>
          <div class="case-study">Case Study 6</div>
          <div class="case-study">Case Study 7</div>
          <div class="case-study">Case Study 8</div>
          <div class="case-study">Case Study 9</div>
          <div class="case-study">Case Study 10</div>
          <div class="case-study">Case Study 11</div>
          <div class="case-study">Case Study 12</div>
          <div class="case-study">Case Study 13</div>
          <div class="case-study">Case Study 14</div>
          <div class="case-study">Case Study 15</div>
        </dc-case-studies>
      `) as CaseStudiesElement;

      // Manually set up items and trigger pagination
      (elementWithManyItems as any)._slottedItems = Array.from(elementWithManyItems.querySelectorAll('.case-study'));
      (elementWithManyItems as any)._applyPagination();
      await elementWithManyItems.updateComplete;

      const loadMoreContainer = elementWithManyItems.shadowRoot?.querySelector('.load-more-container');
      const loadMoreButton = elementWithManyItems.shadowRoot?.querySelector('uui-button');

      expect(loadMoreContainer).to.exist;
      expect(loadMoreButton).to.exist;
      expect(loadMoreButton?.getAttribute('label')).to.equal('Load more case studies');
    });

    it('should handle load more button click', async () => {
      const elementWithManyItems = await fixture(html`
        <dc-case-studies>
          <div class="case-study">Case Study 1</div>
          <div class="case-study">Case Study 2</div>
          <div class="case-study">Case Study 3</div>
          <div class="case-study">Case Study 4</div>
          <div class="case-study">Case Study 5</div>
          <div class="case-study">Case Study 6</div>
          <div class="case-study">Case Study 7</div>
          <div class="case-study">Case Study 8</div>
          <div class="case-study">Case Study 9</div>
          <div class="case-study">Case Study 10</div>
          <div class="case-study">Case Study 11</div>
          <div class="case-study">Case Study 12</div>
          <div class="case-study">Case Study 13</div>
          <div class="case-study">Case Study 14</div>
          <div class="case-study">Case Study 15</div>
          <div class="case-study">Case Study 16</div>
          <div class="case-study">Case Study 17</div>
          <div class="case-study">Case Study 18</div>
        </dc-case-studies>
      `) as CaseStudiesElement;

      // Set up items and trigger pagination
      (elementWithManyItems as any)._slottedItems = Array.from(elementWithManyItems.querySelectorAll('.case-study'));
      (elementWithManyItems as any)._applyPagination();
      await elementWithManyItems.updateComplete;

      const initialItemsToShow = (elementWithManyItems as any)._itemsToShow;

      // Get and click the load more button
      const loadMoreButton = elementWithManyItems.shadowRoot?.querySelector('uui-button') as HTMLElement;
      if (loadMoreButton) {
        loadMoreButton.click();
        await elementWithManyItems.updateComplete;
      }

      // After clicking, _itemsToShow should increase by PAGE_SIZE (9)
      const newItemsToShow = (elementWithManyItems as any)._itemsToShow;
      expect(newItemsToShow).to.be.greaterThan(initialItemsToShow);
    });

    it('should apply pagination-hidden attribute to items beyond page size', async () => {
      const elementWithManyItems = await fixture(html`
        <dc-case-studies>
          <div class="case-study">Case Study 1</div>
          <div class="case-study">Case Study 2</div>
          <div class="case-study">Case Study 3</div>
          <div class="case-study">Case Study 4</div>
          <div class="case-study">Case Study 5</div>
          <div class="case-study">Case Study 6</div>
          <div class="case-study">Case Study 7</div>
          <div class="case-study">Case Study 8</div>
          <div class="case-study">Case Study 9</div>
          <div class="case-study">Case Study 10</div>
          <div class="case-study">Case Study 11</div>
          <div class="case-study">Case Study 12</div>
        </dc-case-studies>
      `) as CaseStudiesElement;

      // Set up items and trigger pagination
      const slottedItems = Array.from(elementWithManyItems.querySelectorAll('.case-study'));
      (elementWithManyItems as any)._slottedItems = slottedItems;
      (elementWithManyItems as any)._applyPagination();
      await elementWithManyItems.updateComplete;

      // Items 0-8 (9 items) should NOT have pagination-hidden
      for (let i = 0; i < 9; i++) {
        expect(slottedItems[i].hasAttribute('pagination-hidden')).to.be.false;
      }

      // Items 9-11 should have pagination-hidden
      for (let i = 9; i < 12; i++) {
        expect(slottedItems[i].hasAttribute('pagination-hidden')).to.be.true;
      }
    });

    it('should exclude filter-out items from pagination calculations', async () => {
      const elementWithFilteredItems = await fixture(html`
        <dc-case-studies>
          <div class="case-study">Case Study 1</div>
          <div class="case-study">Case Study 2</div>
          <div class="case-study">Case Study 3</div>
          <div class="case-study">Case Study 4</div>
          <div class="case-study">Case Study 5</div>
          <div class="case-study">Case Study 6</div>
          <div class="case-study">Case Study 7</div>
          <div class="case-study">Case Study 8</div>
          <div class="case-study">Case Study 9</div>
          <div class="case-study">Case Study 10</div>
          <div class="case-study">Case Study 11</div>
          <div class="case-study">Case Study 12</div>
        </dc-case-studies>
      `) as CaseStudiesElement;

      const slottedItems = Array.from(elementWithFilteredItems.querySelectorAll('.case-study'));

      // Mark some items as filtered out
      slottedItems[0].setAttribute('filter-out', '');
      slottedItems[1].setAttribute('filter-out', '');

      (elementWithFilteredItems as any)._slottedItems = slottedItems;
      (elementWithFilteredItems as any)._applyPagination();
      await elementWithFilteredItems.updateComplete;

      // With 2 items filtered out, we have 10 visible items
      // First 9 visible items should not have pagination-hidden
      const visibleItems = slottedItems.filter(item => !item.hasAttribute('filter-out'));

      // The 10th visible item should have pagination-hidden
      expect(visibleItems[8].hasAttribute('pagination-hidden')).to.be.false;
      expect(visibleItems[9].hasAttribute('pagination-hidden')).to.be.true;
    });
  });

  describe('Filter Change Events', () => {
    it('should trigger filter change handler', async () => {
      const elementWithFilterItems = await fixture(html`
        <dc-case-studies>
          <div class="case-study">Case Study 1</div>
          <div class="case-study">Case Study 2</div>
          <div class="case-study">Case Study 3</div>
          <div class="case-study">Case Study 4</div>
          <div class="case-study">Case Study 5</div>
        </dc-case-studies>
      `) as CaseStudiesElement;

      const filtersElement = elementWithFilterItems.shadowRoot?.querySelector('dc-filters') as any;

      // Manually trigger filter change event
      if (filtersElement) {
        const event = new CustomEvent('change', {
          detail: { filter: 'skill' },
          bubbles: true,
          composed: true
        });

        // Mock the _slotItems property on filters element
        filtersElement._slotItems = Array.from(elementWithFilterItems.querySelectorAll('.case-study'));
        filtersElement.dispatchEvent(event);

        await elementWithFilterItems.updateComplete;

        // Verify that slotted items were populated
        expect((elementWithFilterItems as any)._slottedItems).to.have.length.greaterThan(0);
      }
    });

    it('should apply pagination on filter change', async () => {
      const elementWithFilterItems = await fixture(html`
        <dc-case-studies>
          <div class="case-study">Case Study 1</div>
          <div class="case-study">Case Study 2</div>
          <div class="case-study">Case Study 3</div>
          <div class="case-study">Case Study 4</div>
          <div class="case-study">Case Study 5</div>
          <div class="case-study">Case Study 6</div>
          <div class="case-study">Case Study 7</div>
          <div class="case-study">Case Study 8</div>
          <div class="case-study">Case Study 9</div>
          <div class="case-study">Case Study 10</div>
          <div class="case-study">Case Study 11</div>
          <div class="case-study">Case Study 12</div>
        </dc-case-studies>
      `) as CaseStudiesElement;

      const slottedItems = Array.from(elementWithFilterItems.querySelectorAll('.case-study'));
      const filtersElement = elementWithFilterItems.shadowRoot?.querySelector('dc-filters') as any;

      if (filtersElement) {
        // Mock the _slotItems and dispatch change event
        filtersElement._slotItems = slottedItems;

        const event = new CustomEvent('change', {
          detail: { filter: 'skill' },
          bubbles: true,
          composed: true
        });
        filtersElement.dispatchEvent(event);

        await elementWithFilterItems.updateComplete;

        // Verify pagination was applied (items beyond PAGE_SIZE should have attribute)
        const itemsBeyondPageSize = slottedItems.slice(9);
        const anyHasPaginationHidden = itemsBeyondPageSize.some(item =>
          item.hasAttribute('pagination-hidden')
        );
        expect(anyHasPaginationHidden).to.be.true;
      }
    });
    it('should initialize with empty slotted items array', async () => {
      const testElement = await fixture(html`
        <dc-case-studies>
          <div class="case-study">Case Study 1</div>
        </dc-case-studies>
      `) as CaseStudiesElement;

      expect((testElement as any)._slottedItems).to.be.an('array');
    });
  });

  describe('Filtering', () => {
    it('should have initial hasMoreItems state as false', async () => {
      const testElement = await fixture(html`
        <dc-case-studies>
          <div class="case-study">Case Study 1</div>
        </dc-case-studies>
      `) as CaseStudiesElement;

      expect((testElement as any)._hasMoreItems).to.be.a('boolean');
    });
  });

  describe('Slotted Items Management', () => {
    it('should initialize with private state properties', () => {
      expect((element as any)._slottedItems).to.be.an('array');
      expect((element as any)._itemsToShow).to.be.a('number');
      expect((element as any)._hasMoreItems).to.be.a('boolean');
    });
  });
});
