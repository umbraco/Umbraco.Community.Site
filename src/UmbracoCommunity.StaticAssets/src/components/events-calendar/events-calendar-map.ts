import { LitElement, html, css } from 'lit';
import { customElement, property, state } from 'lit/decorators.js';
import { MapMarker } from '@umbraco-community/types/events-calendar/event';
import { setOptions, importLibrary } from '@googlemaps/js-api-loader';
import { MarkerClusterer, SuperClusterAlgorithm } from '@googlemaps/markerclusterer';
import { buildEventPopup } from '@umbraco-community/util/events-calendar-popup';
import { createLogger } from '@umbraco-community/util/logger';

const logger = createLogger('events-calendar');

const MARKER_COLORS = {
  RSS: '#1b264f',
  AIRTABLE: '#283a97',
  DEFAULT: '#007BFF'
} as const;

const CLUSTER_COLORS = {
  SMALL: { background: '#283a97', text: '#ffffff', size: 30 },
  MEDIUM: { background: '#283a97', text: '#ffffff', size: 35 },
  LARGE: { background: '#283a97', text: '#ffffff', size: 40 }
} as const;

interface GoogleMapConfig {
  provider: string;
  apiKey?: string;
  mapOptions?: {
    zoom: number;
    center: { lat: number, lng: number };
    mapTypeId: string;
    mapId?: string;
    gestureHandling?: string;
  };
}

@customElement('events-calendar-map')
export class EventsCalendarMap extends LitElement {
  @property({ type: Array }) markers: MapMarker[] = [];
  @state() private selectedMarker: MapMarker | null = null;
  @state() private mapError = false;
  @state() private isMapLoading = true;
  @state() private mapProvider: 'google' | 'error' = 'google';
  
  private map: google.maps.Map | null = null;
  private googleMarkers: Array<google.maps.Marker | google.maps.marker.AdvancedMarkerElement> = [];
  private markerClusterer: MarkerClusterer | null = null;
  private infoWindow: google.maps.InfoWindow | null = null;
  private mapConfig: GoogleMapConfig | null = null;
  private isInitialLoad = true;
  private lastFocusedElement: HTMLElement | null = null;
  private infoWindowFocusCleanup: (() => void) | null = null;

  static styles = css`
    :host {
      display: block;
    }

    .map-container {
      background-color: var(--color-identity-white);
      border: 0.0625rem solid var(--color-light-grey);
      border-radius: var(--border-radius-lg);
      margin-bottom: var(--unit-md);
    }

    .map-title {
      font-size: var(--font-size-h2);
      font-weight: var(--font-weight-semibold);
      color: var(--color-identity-darkest);
      margin: 0 0 var(--unit-sm) 0;
      display: flex;
      align-items: center;
      gap: var(--unit-xs);
    }

    .provider-badge {
      background: var(--color-accent);
      color: white;
      padding: 0.125rem 0.5rem;
      border-radius: 0.75rem;
      font-size: 0.7em;
      text-transform: uppercase;
      font-weight: bold;
    }

    .map-wrapper {
      position: relative;
      width: 100%;
      height: 28.75rem;
      border-radius: var(--border-radius);
      overflow: hidden;
      z-index: 1;
    }

    #map {
      width: 100%;
      height: 100%;
      border-radius: var(--border-radius);
    }

    .loading-state {
      display: flex;
      align-items: center;
      justify-content: center;
      height: 28.75rem;
      background: var(--color-secondary-bg);
      border-radius: var(--border-radius);
    }

    .loading-spinner {
      width: 3rem;
      height: 3rem;
      border-radius: 50%;
      border: 0.35rem solid rgba(40, 58, 151, 0.15);
      border-top-color: #283a97;
      animation: map-spinner 1s linear infinite;
    }

    .sr-only {
      position: absolute;
      width: 1px;
      height: 1px;
      padding: 0;
      margin: -1px;
      overflow: hidden;
      clip: rect(0, 0, 0, 0);
      border: 0;
    }

    @keyframes map-spinner {
      0% {
        transform: rotate(0deg);
      }
      100% {
        transform: rotate(360deg);
      }
    }

    .error-state {
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      height: 28.75rem;
      background: var(--color-error-bg, #fee);
      border-radius: var(--border-radius);
      color: var(--color-error-text, #c33);
      text-align: center;
      padding: var(--unit-md);
    }

    .error-title {
      font-size: var(--font-size-lg);
      font-weight: var(--font-weight-semibold);
      margin-bottom: var(--unit-xs);
    }

    .error-message {
      font-size: var(--font-size-sm);
      opacity: 0.8;
      max-width: 18.75rem;
    }

    .retry-button {
      background: var(--color-accent);
      color: white;
      border: none;
      padding: var(--unit-xs) var(--unit-sm);
      border-radius: var(--border-radius);
      cursor: pointer;
      margin-top: var(--unit-sm);
      font-size: var(--font-size-sm);
    }

    .retry-button:hover {
      background: var(--color-accent-hover, var(--color-accent));
      opacity: 0.9;
    }

    /* Mobile responsive adjustments */
    @media (max-width: 768px) {
      .map-wrapper {
        height: 18.75rem;
      }
      
      .map-container {
        margin-bottom: var(--unit-sm);
      }
    }
    
    /* Global popup styles */
    .popup-title {
      font-size: 1rem;
    }
    
    .popup-address,
    .popup-location,
    .popup-date {
      font-size: 0.875rem;
    }
    
    /* Mobile responsive popup styles */
    @media (max-width: 768px) {
      .popup-title {
        font-size: 0.75rem;
      }
      
      .popup-address,
      .popup-location,
      .popup-date {
        font-size: 0.625rem;
      }
    }
  `;

  async connectedCallback() {
    super.connectedCallback();
    this.injectInfoWindowStyles();
    await this.initializeMap();
  }

  disconnectedCallback() {
    super.disconnectedCallback();
    this.cleanup();
  }

  private async initializeMap() {
    try {
      this.isMapLoading = true;
      this.mapError = false;
      
      // Fetch map configuration from backend
      this.mapConfig = await this.fetchMapConfig();
      
      if (this.mapConfig.provider === 'google' && this.mapConfig.apiKey) {
        await this.initializeGoogleMaps();
        this.mapProvider = 'google';
      } else {
        throw new Error('Google Maps not available - no fallback configured');
      }
      
    } catch (error) {
      this.handleMapError(error);
    }
  }

  private async fetchMapConfig(): Promise<GoogleMapConfig> {
    const response = await fetch('/api/events-calendar/map-config');
    if (!response.ok) {
      throw new Error(`Failed to fetch map config: ${response.status}`);
    }
    return await response.json();
  }

  private async initializeGoogleMaps() {
    if (!this.mapConfig?.apiKey) {
      throw new Error('Google Maps API key not provided');
    }

    try {
      logger.debug('Loading Google Maps API...');

      // Set options for the Maps JavaScript API
      setOptions({
        key: this.mapConfig.apiKey,
        v: 'weekly',
        libraries: ['geometry']
      });

      // Import the maps library
      await importLibrary('maps');

      // Attempt to load the advanced marker library; ignore failures and fall back gracefully
      try {
        await importLibrary('marker');
      } catch (markerError) {
        logger.warn('Advanced marker library unavailable', {
          error: markerError instanceof Error ? markerError.message : String(markerError)
        });
      }
      
      logger.info('Google Maps API loaded successfully');
      
      // Wait for the map container to be in DOM
      await this.updateComplete;
      
      const mapElement = this.shadowRoot?.getElementById('map');
      if (!mapElement) {
        throw new Error('Map container element not found');
      }

      logger.info('Initializing Google Map...');

      // Initialize Google Map
      const mapOptions: google.maps.MapOptions = {
        zoom: 2,
        center: { lat: 20, lng: 0 }, // World center
        mapTypeId: google.maps.MapTypeId.ROADMAP,
        gestureHandling: 'greedy',
        renderingType: google.maps.RenderingType.RASTER, // Use raster to avoid WebGL warnings
        ...this.mapConfig.mapOptions
      };

      // Only apply custom styles if no mapId is present
      if (!this.mapConfig.mapOptions?.mapId) {
        mapOptions.styles = this.getMapStyles();
      }

      this.map = new google.maps.Map(mapElement, mapOptions);

      // Initialize InfoWindow for popups with headerDisabled to remove spacing
      this.infoWindow = new google.maps.InfoWindow({
        headerDisabled: true
      });

      this.infoWindow.addListener('closeclick', () => this.teardownInfoWindowAccessibility());

      this.addMarkersToMap();
      
      this.isMapLoading = false;
      
      logger.info('Google Maps initialized successfully', {
        markerCount: this.markers.length
      });
      
    } catch (error) {
      logger.error('Failed to initialize Google Maps', {
        error: error instanceof Error ? error.message : 'Unknown error',
        stack: error instanceof Error ? error.stack : undefined,
        provider: 'google'
      });
      throw error;
    }
  }

  private getMapStyles(): google.maps.MapTypeStyle[] {
    // Optional: Custom map styling for better appearance
    return [
      {
        featureType: 'water',
        elementType: 'geometry.fill',
        stylers: [
          {
            color: '#a2daf2'
          }
        ]
      },
      {
        featureType: 'landscape.natural',
        elementType: 'geometry.fill',
        stylers: [
          {
            color: '#f5f5f2'
          }
        ]
      }
    ];
  }

  private addMarkersToMap() {
    if (!this.map) return;

    // Clear existing markers and clusterer
    this.clearMarkers();

    const hasMapId = Boolean(this.mapConfig?.mapOptions?.mapId);
    const advancedMarkersSupported = this.supportsAdvancedMarkers();
    if (!advancedMarkersSupported) {
      const reason = hasMapId
        ? 'Advanced marker library unavailable; falling back to classic markers'
        : 'Advanced markers require a Google Maps Map ID (vector map). Falling back to classic markers';
      logger.warn(reason);
    }

    const markers = this.markers
      .filter(marker => marker.coordinates && Array.isArray(marker.coordinates) && marker.coordinates.length === 2)
      .map(marker => advancedMarkersSupported
        ? this.createAdvancedMarker(marker)
        : this.createLegacyMarker(marker)
      );

    // Store markers for cleanup
    this.googleMarkers = markers;

    if (advancedMarkersSupported) {
      this.markerClusterer = new MarkerClusterer({
        map: this.map,
        markers: markers as google.maps.marker.AdvancedMarkerElement[],
        algorithm: new SuperClusterAlgorithm({
          radius: 60,
          maxZoom: 15
        }),
        renderer: {
          render: ({ count, position }) => {
            const clusterMarker = this.createAdvancedClusterMarker(count, position.toJSON());
            clusterMarker.zIndex = 1000 + count;
            return clusterMarker;
          }
        }
      });
    } else {
      this.markerClusterer = new MarkerClusterer({
        map: this.map,
        markers: markers as google.maps.Marker[],
        algorithm: new SuperClusterAlgorithm({
          radius: 60,
          maxZoom: 15
        }),
        renderer: {
          render: ({ count, position }) => {
            const clusterData = this.getClusterData(count);
            return new google.maps.Marker({
              position,
              icon: clusterData.icon,
              label: {
                text: count.toString(),
                color: clusterData.textColor,
                fontWeight: 'bold',
                fontSize: '0.75rem'
              },
              zIndex: 1000 + count
            });
          }
        }
      });
    }

    // Fit map to show all markers only on initial load
    if (markers.length > 0 && this.isInitialLoad) {
      const bounds = new google.maps.LatLngBounds();
      const AdvancedMarkerElement = google.maps.marker?.AdvancedMarkerElement;
      markers.forEach(marker => {
        if (marker instanceof google.maps.Marker) {
          const position = marker.getPosition();
          if (position) bounds.extend(position);
        } else if (AdvancedMarkerElement && marker instanceof AdvancedMarkerElement) {
          const position = marker.position;
          if (position) bounds.extend(position as google.maps.LatLng | google.maps.LatLngLiteral);
        }
      });

      this.map?.fitBounds(bounds);

      google.maps.event.addListenerOnce(this.map, 'bounds_changed', () => {
        const zoom = this.map?.getZoom();
        if (zoom && zoom > 10) {
          this.map?.setZoom(10);
        }
      });

      this.isInitialLoad = false;
    }

    logger.info('Markers clustered successfully', {
      totalMarkers: markers.length,
      clustererEnabled: !!this.markerClusterer
    });
  }

  private supportsAdvancedMarkers(): boolean {
    if (!this.mapConfig?.mapOptions?.mapId) {
      return false;
    }
    return typeof google !== 'undefined' && !!google.maps.marker && !!google.maps.marker.AdvancedMarkerElement;
  }

  private createAdvancedMarker(marker: MapMarker): google.maps.marker.AdvancedMarkerElement {
    const position = {
      lat: marker.coordinates![1],
      lng: marker.coordinates![0]
    };

    const pin = new google.maps.marker.PinElement({
      background: this.getMarkerColor(marker.source),
      borderColor: '#ffffff',
      glyphColor: '#ffffff',
      scale: 1.2
    });

    const advancedMarker = new google.maps.marker.AdvancedMarkerElement({
      position,
      title: marker.eventName,
      content: pin.element
    });

    advancedMarker.addListener('click', () => {
      this.showMarkerPopup(advancedMarker, marker);
    });

    return advancedMarker;
  }

  private createLegacyMarker(marker: MapMarker): google.maps.Marker {
    const googleMarker = new google.maps.Marker({
      position: {
        lat: marker.coordinates![1],
        lng: marker.coordinates![0]
      },
      title: marker.eventName,
      icon: this.getMarkerIcon(marker.source)
    });

    googleMarker.addListener('click', () => {
      this.showMarkerPopup(googleMarker, marker);
    });

    return googleMarker;
  }

  private createAdvancedClusterMarker(count: number, position: google.maps.LatLngLiteral): google.maps.marker.AdvancedMarkerElement {
    const { size, background, textColor } = this.getClusterVisuals(count);
    const clusterElement = document.createElement('div');
    clusterElement.style.width = `${size}px`;
    clusterElement.style.height = `${size}px`;
    clusterElement.style.background = background;
    clusterElement.style.color = textColor;
    clusterElement.style.borderRadius = '50%';
    clusterElement.style.display = 'flex';
    clusterElement.style.alignItems = 'center';
    clusterElement.style.justifyContent = 'center';
    clusterElement.style.fontSize = '0.75rem';
    clusterElement.style.fontWeight = 'bold';
    clusterElement.style.boxShadow = '0 0 0 3px rgba(255,255,255,0.8)';
    clusterElement.textContent = count.toString();

    return new google.maps.marker.AdvancedMarkerElement({
      position,
      content: clusterElement
    });
  }

  private getMarkerIcon(source?: string): google.maps.Symbol {
    return {
      path: google.maps.SymbolPath.CIRCLE,
      scale: 10,
      fillColor: this.getMarkerColor(source),
      fillOpacity: 0.9,
      strokeWeight: 2,
      strokeColor: '#ffffff'
    };
  }

  private getClusterData(count: number): { icon: google.maps.Symbol, textColor: string } {
    let size: number;
    let color: string;
    let textColor: string;

    if (count < 5) {
      ({ size, background: color, text: textColor } = CLUSTER_COLORS.SMALL);
    } else if (count < 10) {
      ({ size, background: color, text: textColor } = CLUSTER_COLORS.MEDIUM);
    } else {
      ({ size, background: color, text: textColor } = CLUSTER_COLORS.LARGE);
    }

    const icon: google.maps.Symbol = {
      path: google.maps.SymbolPath.CIRCLE,
      scale: size / 2,
      fillColor: color,
      fillOpacity: 0.8,
      strokeWeight: 3,
      strokeColor: '#ffffff',
      strokeOpacity: 1
    };

    return { icon, textColor };
  }

  private getMarkerColor(source?: string): string {
    if (source === 'rss') {
      return MARKER_COLORS.RSS;
    }
    if (source === 'airtable') {
      return MARKER_COLORS.AIRTABLE;
    }
    return MARKER_COLORS.DEFAULT;
  }

  private getClusterVisuals(count: number): { size: number; background: string; textColor: string } {
    if (count < 5) {
      const { size, background, text } = CLUSTER_COLORS.SMALL;
      return { size, background, textColor: text };
    }
    if (count < 10) {
      const { size, background, text } = CLUSTER_COLORS.MEDIUM;
      return { size, background, textColor: text };
    }
    const { size, background, text } = CLUSTER_COLORS.LARGE;
    return { size, background, textColor: text };
  }

  private showMarkerPopup(googleMarker: google.maps.Marker | google.maps.marker.AdvancedMarkerElement, marker: MapMarker) {
    if (!this.infoWindow) return;

    const wrapperDiv = document.createElement('div');
    wrapperDiv.style.cssText = `
      max-width: 18.75rem;
      font-family: system-ui, -apple-system, sans-serif;
      margin: 0;
      padding: 0;
      line-height: 1.4;
    `;
    
    const headerRow = document.createElement('div');
    headerRow.style.cssText = `
      display: flex;
      justify-content: space-between;
      align-items: flex-start;
      gap: 0.5rem;
      margin-bottom: 0.5rem;
    `;
    
    const titleContainer = document.createElement('div');
    titleContainer.style.cssText = `
      flex: 1;
      min-width: 0;
    `;
    
    // Get the popup content and collect all elements
    const popupElement = buildEventPopup(marker);
    const allElements = Array.from(popupElement.children);
    const titleElement = allElements[0] as HTMLElement;
    const contentElements = allElements.slice(1); // address, location, date
    
    if (titleElement) {
      // Clone title to preserve original structure, then move to title container
      const titleClone = titleElement.cloneNode(true) as HTMLElement;
      titleClone.style.margin = '0';
      titleContainer.appendChild(titleClone);
    }
    
    const closeButton = document.createElement('button');
    closeButton.type = 'button';
    closeButton.innerHTML = '×';
    closeButton.setAttribute('aria-label', 'Close event details');
    closeButton.style.cssText = `
      flex-shrink: 0;
      width: 1.5rem;
      height: 1.5rem;
      border: none;
      border-radius: 50%;
      background: rgba(255, 255, 255, 0.95);
      cursor: pointer;
      font-size: 1.125rem;
      color: #666;
      display: flex;
      align-items: center;
      justify-content: center;
      margin: 0;
      padding: 0;
    `;
    
    closeButton.addEventListener('click', () => {
      if (this.infoWindow) {
        this.infoWindow.close();
      }
      this.teardownInfoWindowAccessibility();
    });
    
    // Build the header row
    headerRow.appendChild(titleContainer);
    headerRow.appendChild(closeButton);
    
    const contentContainer = document.createElement('div');
    contentContainer.style.cssText = `
      margin: 0;
    `;
    
    contentElements.forEach(element => {
      contentContainer.appendChild(element.cloneNode(true));
    });
    
    // Assemble the final popup
    wrapperDiv.appendChild(headerRow);
    wrapperDiv.appendChild(contentContainer);
    
    this.infoWindow.setContent(wrapperDiv);
    this.infoWindow.open({
      map: this.map!,
      anchor: googleMarker,
      shouldFocus: true
    });
    this.selectedMarker = marker;

    this.applyInfoWindowAccessibility(wrapperDiv);

    logger.debug('Marker popup opened', { 
      title: marker.eventName,
      source: marker.source 
    });
  }

  private injectInfoWindowStyles() {
    // Inject global CSS to override Google Maps InfoWindow styles
    const existingStyle = document.getElementById('google-maps-infowindow-fix');
    if (existingStyle) return; // Already injected

    const style = document.createElement('style');
    style.id = 'google-maps-infowindow-fix';
    style.textContent = `
      /* Minimal InfoWindow styling with headerDisabled option */
      .gm-style-iw {
        padding: 0.75rem;
      }
    `;

    document.head.appendChild(style);
  }


  private clearMarkers() {
    // Clear clusterer first
    if (this.markerClusterer) {
      this.markerClusterer.clearMarkers();
      this.markerClusterer = null;
    }
    
    // Clear individual markers
    const AdvancedMarkerElement = google.maps.marker?.AdvancedMarkerElement;
    this.googleMarkers.forEach(marker => {
      if (AdvancedMarkerElement && marker instanceof AdvancedMarkerElement) {
        marker.map = null;
      } else if (marker instanceof google.maps.Marker) {
        marker.setMap(null);
      }
    });
    this.googleMarkers = [];
  }

  private handleMapError(error: unknown) {
    this.mapError = true;
    this.isMapLoading = false;
    this.mapProvider = 'error';
    
    // Log detailed error information
    if (error instanceof Error) {
      logger.error('Map Error Details:', {
        message: error.message,
        stack: error.stack,
        name: error.name
      });
    } else {
      logger.error('Map Error (non-Error object):', error as Record<string, unknown>);
    }
    
    logger.error('Map initialization failed', { 
      error: error instanceof Error ? error.message : 'Unknown error',
      errorType: typeof error,
      errorConstructor: error?.constructor?.name
    });
  }

  private applyInfoWindowAccessibility(container: HTMLElement) {
    this.teardownInfoWindowAccessibility();

    this.lastFocusedElement = document.activeElement instanceof HTMLElement ? document.activeElement : null;

    if (!container.hasAttribute('role')) {
      container.setAttribute('role', 'dialog');
      container.setAttribute('aria-label', 'Event details');
    }

    const focusableSelector = 'button, [href], input, select, textarea, [tabindex]:not([tabindex="-1"])';
    const focusableElements = Array.from(container.querySelectorAll<HTMLElement>(focusableSelector))
      .filter(element => !element.hasAttribute('disabled') && element.tabIndex !== -1);
    const focusTargets = focusableElements.length > 0 ? focusableElements : [container];

    if (focusableElements.length === 0) {
      container.tabIndex = -1;
    }

    const handleKeydown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        event.preventDefault();
        this.infoWindow?.close();
        this.teardownInfoWindowAccessibility();
        return;
      }

      if (event.key === 'Tab') {
        const first = focusTargets[0];
        const last = focusTargets[focusTargets.length - 1];

        if (event.shiftKey && document.activeElement === first) {
          event.preventDefault();
          last.focus();
        } else if (!event.shiftKey && document.activeElement === last) {
          event.preventDefault();
          first.focus();
        }
      }
    };

    container.addEventListener('keydown', handleKeydown);

    requestAnimationFrame(() => {
      focusTargets[0].focus();
    });

    this.infoWindowFocusCleanup = () => {
      container.removeEventListener('keydown', handleKeydown);

      if (this.lastFocusedElement && document.contains(this.lastFocusedElement)) {
        this.lastFocusedElement.focus();
      }

      this.lastFocusedElement = null;
      this.infoWindowFocusCleanup = null;
    };
  }

  private teardownInfoWindowAccessibility() {
    if (this.infoWindowFocusCleanup) {
      this.infoWindowFocusCleanup();
    }
  }

  private cleanup() {
    this.clearMarkers();
    this.teardownInfoWindowAccessibility();
    if (this.infoWindow) {
      this.infoWindow.close();
    }
    this.map = null;
    this.infoWindow = null;
    this.markerClusterer = null;
  }

  private async retryMapInitialization() {
    logger.info('Retrying map initialization');
    await this.initializeMap();
  }

  // Update markers when property changes
  updated(changedProperties: Map<string | number | symbol, unknown>) {
    if (changedProperties.has('markers') && this.map && !this.isMapLoading) {
      this.addMarkersToMap();
    }
  }

  render() {
    return html`
      <div class="map-container">
        <div class="map-wrapper">
          <div id="map" style="display: ${this.isMapLoading || this.mapError ? 'none' : 'block'}"></div>
          
          ${this.isMapLoading ? html`
            <div class="loading-state" role="status" aria-live="polite">
              <div class="loading-spinner">
                <span class="sr-only">Loading interactive map</span>
              </div>
            </div>
          ` : this.mapError ? html`
            <div class="error-state">
              <div class="error-title">Map Unavailable</div>
              <div class="error-message">
                Unable to load the interactive map. Please check your connection and try again.
              </div>
              <button class="retry-button" @click=${this.retryMapInitialization}>
                Retry
              </button>
            </div>
          ` : ''}
        </div>
      </div>
    `;
  }
}
