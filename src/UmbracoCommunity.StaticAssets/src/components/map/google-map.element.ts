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

  @property({ type: Array })
  styles: Array<google.maps.MapTypeStyle> = [];

  @state()
  private _markers: Array<google.maps.Marker> = [];

  #mapRef?: google.maps.Map;
  #clusterer?: MarkerClusterer;

  readonly #apiKey = "AIzaSyBetZwWScNRk0KrDKJJxiWNqig1TtXMASo";

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

    const googleMapsLoader = document.createElement("script");
    googleMapsLoader.src = `https://maps.googleapis.com/maps/api/js?key=${
      this.#apiKey
    }&callback=initMap`;
    googleMapsLoader.id = "google-maps-loader";
    googleMapsLoader.async = true;
    googleMapsLoader.defer = true;

    this.shadowRoot?.appendChild(googleMapsLoader);
  }

  #mapReady = async () => {
    const { Map } = (await google.maps.importLibrary(
      "maps"
    )) as google.maps.MapsLibrary;

    this.#mapRef = new Map(
      this.shadowRoot?.getElementById("map") as HTMLElement,
      {
        center: { lat: -34.397, lng: 150.644 },
        zoom: 4,
        styles: this.styles,
        fullscreenControl: false,
        streetViewControl: false,
        mapTypeControl: false,
      }
    );

    if (this.markers.length) {
      this.#removeMarkers();
      this.#addMarkers();
    }

    window.mapsLoaded = true;
  };

  async #removeMarkers() {
    this._markers.forEach((m) => m.setMap(null));
    this._markers = [];
    this.#clusterer?.clearMarkers();
  }

  async #addMarkers() {
    const { InfoWindow } = (await google.maps.importLibrary(
      "maps"
    )) as google.maps.MapsLibrary;
    const { Marker } = (await google.maps.importLibrary(
      "marker"
    )) as google.maps.MarkerLibrary;

    const infoWindow = new InfoWindow();

    for (var i = 0; i < this.markers.length; i += 1) {
      const m = this.markers[i];
      const map = this.#mapRef;

      const marker = new Marker({
        position: m.position,
        map,
        title: m.name(),
      });

      if (m.icon) {
        marker.setIcon(m.icon);
      }

      google.maps.event.addListener(
        marker,
        "click",
        (function (marker: google.maps.Marker): any {
          return function () {
            infoWindow.close();
            infoWindow.setContent(m.content);
            infoWindow.open({ map, anchor: marker });
          };
        })(marker)
      );

      this._markers.push(marker);
    }

    const renderer = {
      render: ({ count, position }: { count: number; position: any }) =>
        new google.maps.Marker({
          label: { text: String(count), color: "white", fontSize: "12px" },
          position,
          // adjust zIndex to be above other markers
          zIndex: Number(google.maps.Marker.MAX_ZINDEX) + count,
          icon: new URL("../../assets/pin-clusterer.svg", import.meta.url).href,
        }),
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
