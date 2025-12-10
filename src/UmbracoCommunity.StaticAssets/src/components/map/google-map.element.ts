import { MarkerClusterer } from "@googlemaps/markerclusterer";
import { LitElement, PropertyValueMap, css, html } from "lit";
import { customElement, property, state } from "lit/decorators.js";
import { MapMarkerModel } from "./entities";

window.initMap = () => {
  window.dispatchEvent(new CustomEvent("google-map-ready"));
};

window.mapsLoaded = false;

const elementName = "dc-google-map";

@customElement(elementName)
export class DcGoogleMapElement extends LitElement {
  @property({ type: Array })
  markers: Array<MapMarkerModel> = [];

  @state()
  private _markers: Array<google.maps.marker.AdvancedMarkerElement> = [];

  #mapRef?: google.maps.Map;
  #clusterer?: MarkerClusterer;

  readonly #apiKey = "AIzaSyCfWeM7oKe9uV35obF62mzU57dsOLo8NGI";

  constructor() {
    super();
    window.addEventListener("google-map-ready", this.#mapReady);
  }

  firstUpdated() {
    this.#addScriptTag();
  }

  protected updated(
    _changedProperties: PropertyValueMap<any> | Map<PropertyKey, unknown>
  ): void {
    if (!_changedProperties.has("markers") || !window.mapsLoaded) {
      return;
    }
    
    this.#removeMarkers();
    this.#addMarkers();
    super.updated(_changedProperties);
  }

  disconnectedCallback(): void {
    window.removeEventListener("google-map-ready", this.#mapReady);
    super.disconnectedCallback();
  }

  #addScriptTag() {
    if (window.mapsLoaded) {
      window.dispatchEvent(new CustomEvent("google-map-ready"));
      return;
    }

    const params = new URLSearchParams({
      key: this.#apiKey,
      loading: "async",
      callback: "initMap",
      libraries: "marker",
      v: "weekly" // Ensure we're using the latest weekly version
    });

    const googleMapsLoader = document.createElement("script");
    googleMapsLoader.src = `https://maps.googleapis.com/maps/api/js?${params.toString()}`;
    googleMapsLoader.id = "google-maps-loader";
    googleMapsLoader.async = true;
    googleMapsLoader.defer = true;

    this.shadowRoot?.appendChild(googleMapsLoader);
  }

  #mapReady = async () => {
    const { Map } = (await google.maps.importLibrary(
      "maps"
    )) as google.maps.MapsLibrary;

    // Wait for the map library to be fully loaded
    await google.maps.importLibrary("core");

    this.#mapRef = new Map(
      this.shadowRoot?.getElementById("map") as HTMLElement,
      {
        center: { lat: -34.397, lng: 150.644 },
        zoom: 4,
        fullscreenControl: false,
        streetViewControl: false,
        mapTypeControl: false,
        mapId: "6b9fb9d100b5a19ae5fe7f7d", // Required for AdvancedMarkerElement
        gestureHandling: "greedy", // Allow scrolling without ctrl
        colorScheme: "DARK", // Set dark color scheme
      }
    );

    if (this.markers.length) {
      this.#removeMarkers();
      this.#addMarkers();
    }

    window.mapsLoaded = true;
  };

  async #removeMarkers() {
    this._markers.forEach((m) => (m.map = null));
    this._markers = [];
    this.#clusterer?.clearMarkers();
  }

  async #addMarkers() {
    const { InfoWindow } = (await google.maps.importLibrary(
      "maps"
    )) as google.maps.MapsLibrary;
    const { AdvancedMarkerElement, PinElement } = (await google.maps.importLibrary(
      "marker"
    )) as google.maps.MarkerLibrary;

    const infoWindow = new InfoWindow();

    for (var i = 0; i < this.markers.length; i += 1) {
      const m = this.markers[i];
      const map = this.#mapRef;

      // Create custom pin if icon is specified
      let content;
      if (m.icon) {
        const img = document.createElement("img");
        img.src = m.icon;
        content = img;
      }

      const marker = new AdvancedMarkerElement({
        position: m.position,
        map,
        title: m.name(),
        content: content,
      });

      marker.addListener("click", () => {
        infoWindow.close();
        infoWindow.setContent(m.content);
        infoWindow.open({ map, anchor: marker });
      });

      this._markers.push(marker);
    }

    const renderer = {
      render: ({ count, position }: { count: number; position: any }) => {
        const div = document.createElement("div");
        div.style.position = "relative";
        
        const img = document.createElement("img");
        img.src = new URL("../../assets/pin-clusterer.svg", import.meta.url).href;
        
        const label = document.createElement("div");
        label.textContent = String(count);
        label.style.position = "absolute";
        label.style.top = "50%";
        label.style.left = "50%";
        label.style.transform = "translate(-50%, -50%)";
        label.style.color = "white";
        label.style.fontSize = "12px";
        label.style.fontWeight = "bold";
        
        div.appendChild(img);
        div.appendChild(label);
        
        return new AdvancedMarkerElement({
          position,
          content: div,
          zIndex: 1000 + count,
        });
      },
    };

    this.#clusterer = new MarkerClusterer({
      markers: this._markers,
      map: this.#mapRef,
      renderer,
    });

    this.setBounds();
  }

  async setBounds() {
    const { LatLng, LatLngBounds } = (await google.maps.importLibrary(
      "core"
    )) as google.maps.CoreLibrary;

    if (this.markers.length === 0) {
      // show the whole world if there are no markers
      var worldBounds = new LatLngBounds(
        new LatLng(70.4043, -143.5291), // Top-left
        new LatLng(-46.11251, 163.4288) // Bottom-right
      );
      this.#mapRef?.fitBounds(worldBounds, 0);
    } else if (this.markers.length === 1) {
      this.#mapRef?.setCenter(this.markers[0].position);
      this.#mapRef?.setZoom(15);
    } else {
      var initialBounds = this.markers.reduce((bounds, marker) => {
        bounds.extend(marker.position);
        return bounds;
      }, new LatLngBounds());

      this.#mapRef?.fitBounds(initialBounds);
    }
  }

  render() {
    return html`<div id="map"></div>`;
  }

  static styles = [
    css`
      :host {
        display: block;
      }
      #map {
        width: 100%;
        height: 500px;
      }
    `,
  ];
}

declare global {
  interface HTMLElementTagNameMap {
    [elementName]: DcGoogleMapElement;
  }

  interface Window {
    mapsLoaded: Boolean;
    initMap: () => void;
  }
}
