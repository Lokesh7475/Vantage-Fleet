import { Component, OnInit, OnDestroy, Input, OnChanges, SimpleChanges, Output, EventEmitter } from '@angular/core';
import * as L from 'leaflet';
import { TelemetryService, VehicleTelemetry } from '../services/telemetry.service';
import { HistoryService, RoutePoint } from '../services/history.service';
import { Subscription } from 'rxjs';

@Component({
    selector: 'app-map',
    standalone: true,
    imports: [],
    templateUrl: './map.component.html',
    styleUrl: './map.component.css'
})
export class MapComponent implements OnInit, OnDestroy, OnChanges {
    @Input() selectedTruckId: string | null = null;
    @Input() selectedTripId: string | null = null;
    @Input() isLiveTrip: boolean = false;
    @Output() truckSelected = new EventEmitter<string>();
    private map: L.Map | undefined;
    private markers: Map<string, L.Marker> = new Map();
    private telemetrySub: Subscription | undefined;
    private tripPolyline: L.Polyline | null = null;
    private lastAddedPoint: string | null = null;

    private latestTelemetry: VehicleTelemetry[] = [];

    constructor(
        private telemetryService: TelemetryService,
        private historyService: HistoryService
    ) { }

    ngOnInit(): void {
        this.initMap();

        // Subscribe to mocked telemetry
        this.telemetrySub = this.telemetryService.getTelemetryUpdates().subscribe((dataArray: VehicleTelemetry[]) => {
            this.latestTelemetry = dataArray;
            this.updateMarkers();
        });
    }

    ngOnChanges(changes: SimpleChanges): void {
        if (changes['selectedTruckId']) {
            this.updateMarkers();

            // Auto-pan if selection changed by user in list
            const currentSelection = changes['selectedTruckId'].currentValue;
            if (currentSelection) {
                const telemetry = this.latestTelemetry.find(t => t.id === currentSelection);
                if (telemetry && this.map) {
                    this.map.panTo([telemetry.lat, telemetry.lng], { animate: true, duration: 1.0 });
                }
            }
        }

        if (changes['selectedTripId']) {
            const currentTripId = changes['selectedTripId'].currentValue;
            this.updateTripRoute(currentTripId);
        }
    }

    private updateTripRoute(tripId: string | null): void {
        const currentMap = this.map;
        if (!currentMap) return;

        // Clear existing polyline
        if (this.tripPolyline) {
            this.tripPolyline.remove();
            this.tripPolyline = null;
            this.lastAddedPoint = null;
        }

        if (tripId) {
            this.historyService.getTripRoute(tripId).subscribe({
                next: (routePoints: RoutePoint[]) => {
                    if (routePoints && routePoints.length > 0) {
                        const latlngs = routePoints.map(p => L.latLng(p.latitude, p.longitude));

                        if (latlngs.length > 0) {
                            const lastP = latlngs[latlngs.length - 1];
                            this.lastAddedPoint = `${lastP.lat},${lastP.lng}`;
                        }

                        this.tripPolyline = L.polyline(latlngs, {
                            color: '#ffffff',
                            weight: 6,
                            opacity: 0.9,
                            lineCap: 'round',
                            lineJoin: 'round',
                            className: 'gmaps-trip-path'
                        }).addTo(currentMap);

                        // Zoom map to fit the route
                        currentMap.fitBounds(this.tripPolyline.getBounds(), { padding: [50, 50], animate: true });
                    }
                },
                error: (err) => {
                    console.error('Error fetching trip route:', err);
                }
            });
        }
    }

    private updateMarkers(): void {
        const currentMap = this.map;
        if (!currentMap) return;

        const currentDataIds = new Set(this.latestTelemetry.map(t => t.id));

        // 1. Remove markers that are no longer in the live telemetry stream (offline)
        for (const [id, marker] of this.markers.entries()) {
            if (!currentDataIds.has(id)) {
                marker.remove();
                this.markers.delete(id);
            }
        }

        // 2. Add or update active markers
        this.latestTelemetry.forEach(data => {
            let marker = this.markers.get(data.id);
            const isSelected = data.id === this.selectedTruckId;
            const strokeColor = isSelected ? '#ffed00' : '#00e5ff';
            const shadowColor = isSelected ? 'rgba(255, 237, 0, 0.9)' : 'rgba(0, 229, 255, 0.9)';
            const fillColor = isSelected ? 'rgba(255, 237, 0, 0.3)' : 'rgba(0, 229, 255, 0.3)';

            // SVG template for the truck
            const svgContent = `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="${fillColor}" stroke="${strokeColor}" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" style="filter: drop-shadow(0 0 6px ${shadowColor}); width: 100%; height: 100%; transform: rotate(${data.heading}deg); transition: transform 1s linear;">
                                  <rect x="1" y="3" width="15" height="13"></rect>
                                  <polygon points="16 8 20 8 23 11 23 16 16 16 16 8"></polygon>
                                  <circle cx="5.5" cy="18.5" r="2.5" fill="#111"></circle>
                                  <circle cx="18.5" cy="18.5" r="2.5" fill="#111"></circle>
                                </svg>`;

            if (marker) {
                // Update Marker cleanly without redrawing DOM (from spec requirements to prevent flickering)
                marker.setLatLng([data.lat, data.lng]);

                // Update tooltip content if speed changed
                marker.setTooltipContent(`
                    <div style="font-family: 'Space Mono', monospace; font-size: 11px; color: #fff;">
                        <span style="color: ${strokeColor}; font-weight: bold;">${data.id}</span><br>
                        ${data.speed} km/h
                    </div>
                `);

                // Update the inner HTML of the icon to apply new rotation
                const iconElement = marker.getElement();
                if (iconElement) {
                    iconElement.innerHTML = svgContent;
                }
            } else {
                // Use a custom divIcon for a futuristic neon truck
                const truckIcon = L.divIcon({
                    html: svgContent,
                    className: 'custom-truck-icon transition-movement',
                    iconSize: [28, 28],
                    iconAnchor: [14, 14] // Center the icon
                });

                marker = L.marker([data.lat, data.lng], { icon: truckIcon }).addTo(currentMap);

                // Add techy tooltip
                marker.bindTooltip(`
                    <div style="font-family: 'Space Mono', monospace; font-size: 11px; color: #fff;">
                        <span style="color: ${strokeColor}; font-weight: bold;">${data.id}</span><br>
                        ${data.speed} km/h
                    </div>
                `, {
                    permanent: false,
                    direction: 'top',
                    offset: [0, -10],
                    className: 'tech-tooltip'
                });

                // Emit selection event on click
                marker.on('click', () => {
                    this.truckSelected.emit(data.id);
                });

                this.markers.set(data.id, marker);
            }

            // Auto-pan if the selected truck approaches the edge of the visible map
            if (isSelected && (!this.selectedTripId || this.isLiveTrip)) {
                // Shrink the checking bounds by 15% to create a trigger threshold before it fully leaves
                const bounds = currentMap.getBounds().pad(-0.15);
                const truckLatLng = L.latLng(data.lat, data.lng);

                if (!bounds.contains(truckLatLng)) {
                    // Smoothly pan the map to keep the truck in view
                    currentMap.panTo(truckLatLng, { animate: true, duration: 0.5 });
                }

                // If a path is currently being drawn for THIS vehicle's active trip, append the live point
                if (this.isLiveTrip && this.tripPolyline) {
                    const pointKey = `${truckLatLng.lat},${truckLatLng.lng}`;
                    if (this.lastAddedPoint !== pointKey) {
                        this.tripPolyline.addLatLng(truckLatLng);
                        this.lastAddedPoint = pointKey;
                    }
                }
            }
        });
    }

    private initMap(): void {
        this.map = L.map('map').setView([23.0225, 72.5714], 12);

        // CartoDB Dark Matter for a minimalistic dark theme
        L.tileLayer('https://{s}.basemaps.cartocdn.com/dark_all/{z}/{x}/{y}{r}.png', {
            maxZoom: 19,
            attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors &copy; <a href="https://carto.com/attributions">CARTO</a>'
        }).addTo(this.map);
    }

    ngOnDestroy(): void {
        if (this.telemetrySub) {
            this.telemetrySub.unsubscribe();
        }
        if (this.map) {
            this.map.remove();
        }
    }
}
