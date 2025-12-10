import { expect } from '@open-wc/testing';
import { generatePartnerMarker } from './marker-generator.function.js';

describe('generatePartnerMarker', () => {
  let mockElement: HTMLElement;

  beforeEach(() => {
    // Create a mock HTML element
    mockElement = document.createElement('div');
    const h3 = document.createElement('h3');
    h3.innerText = 'Test Partner';
    const img = document.createElement('img');
    img.setAttribute('slot', 'logo');
    img.src = 'test-logo.png';
    img.alt = 'Test Logo';
    
    mockElement.appendChild(h3);
    mockElement.appendChild(img);
  });

  it('should generate marker for silver partner with coordinates', () => {
    // Arrange
    const partnerCard = {
      coordinates: '55.6761,12.5683',
      level: 'Silver',
      country: 'Denmark',
      color: '#c0c0c0',
      link: 'https://test-partner.com'
    };

    Object.assign(mockElement, partnerCard);

    // Act
    const result = generatePartnerMarker(mockElement as any);

    // Assert
    expect(result).to.not.be.null;
    expect(result!.partner.name).to.equal('Test Partner');
    expect(result!.partner.country).to.equal('Denmark');
    expect(result!.partner.partnership).to.equal('Silver');
    expect(result!.partner.logo).to.include('test-logo.png');
    expect(result!.partner.imageBackgroundColor).to.equal('#c0c0c0');
    expect(result!.partner.url).to.be.undefined; // Silver partners should not have clickable URLs
    expect(result!.position.lat).to.equal(55.6761);
    expect(result!.position.lng).to.equal(12.5683);
    expect(result!.name()).to.equal('Test Partner');
    // Icon should be a valid URL (either file path or data URI)
    expect(result!.icon).to.be.a('string');
    expect(result!.icon.length).to.be.greaterThan(0);
  });

  it('should generate marker for gold partner with clickable URL', () => {
    // Arrange
    const partnerCard = {
      coordinates: '51.5074,-0.1278',
      level: 'Gold',
      country: 'United Kingdom',
      color: '#ffd700',
      link: 'https://gold-partner.com'
    };

    Object.assign(mockElement, partnerCard);

    // Act
    const result = generatePartnerMarker(mockElement as any);

    // Assert
    expect(result).to.not.be.null;
    expect(result!.partner.url).to.equal('https://gold-partner.com');
    // Icon should be a valid URL (either file path or data URI)
    expect(result!.icon).to.be.a('string');
    expect(result!.icon.length).to.be.greaterThan(0);
  });

  it('should generate marker for platinum partner with clickable URL', () => {
    // Arrange
    const partnerCard = {
      coordinates: '40.7128,-74.0060',
      level: 'Platinum',
      country: 'United States',
      color: '#e5e4e2',
      link: 'https://platinum-partner.com'
    };

    Object.assign(mockElement, partnerCard);

    // Act
    const result = generatePartnerMarker(mockElement as any);

    // Assert
    expect(result).to.not.be.null;
    expect(result!.partner.url).to.equal('https://platinum-partner.com');
    // Icon should be a valid URL (either file path or data URI)
    expect(result!.icon).to.be.a('string');
    expect(result!.icon.length).to.be.greaterThan(0);
  });

  it('should return null for invalid coordinates', () => {
    // Arrange
    const partnerCard = {
      coordinates: 'invalid,coordinates',
      level: 'Silver',
      country: 'Test Country',
      color: '#c0c0c0'
    };

    Object.assign(mockElement, partnerCard);

    // Act
    const result = generatePartnerMarker(mockElement as any);

    // Assert
    expect(result).to.be.null;
  });

  it('should return null for missing coordinates', () => {
    // Arrange
    const partnerCard = {
      coordinates: '',
      level: 'Silver',
      country: 'Test Country',
      color: '#c0c0c0'
    };

    Object.assign(mockElement, partnerCard);

    // Act
    const result = generatePartnerMarker(mockElement as any);

    // Assert
    expect(result).to.be.null;
  });

  it('should handle missing partner name gracefully', () => {
    // Arrange
    mockElement.innerHTML = '';
    const img = document.createElement('img');
    img.setAttribute('slot', 'logo');
    img.src = 'test-logo.png';
    img.alt = 'Test Logo';
    mockElement.appendChild(img);
    
    const partnerCard = {
      coordinates: '55.6761,12.5683',
      level: 'Silver',
      country: 'Denmark',
      color: '#c0c0c0'
    };

    Object.assign(mockElement, partnerCard);

    // Act
    const result = generatePartnerMarker(mockElement as any);

    // Assert
    expect(result).to.not.be.null;
    expect(result!.partner.name).to.be.undefined;
    expect(result!.name()).to.equal('');
  });

  it('should handle missing logo gracefully', () => {
    // Arrange
    mockElement.innerHTML = '';
    const h3 = document.createElement('h3');
    h3.innerText = 'Test Partner';
    mockElement.appendChild(h3);
    
    const partnerCard = {
      coordinates: '55.6761,12.5683',
      level: 'Silver',
      country: 'Denmark',
      color: '#c0c0c0'
    };

    Object.assign(mockElement, partnerCard);

    // Act
    const result = generatePartnerMarker(mockElement as any);

    // Assert
    expect(result).to.not.be.null;
    expect(result!.partner.logo).to.be.undefined;
  });
});
