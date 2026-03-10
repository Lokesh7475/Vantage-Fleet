import { Component, OnInit } from '@angular/core';
import { MapComponent } from './map/map.component';
import { AsyncPipe, DecimalPipe, DatePipe } from '@angular/common';
import { TelemetryService, VehicleTelemetry } from './services/telemetry.service';
import { HistoryService, Trip, Vehicle } from './services/history.service';
import { BehaviorSubject, Observable, combineLatest, map, timer } from 'rxjs';

export interface FleetVehicle extends Vehicle {
    isOnline: boolean;
    telemetry?: VehicleTelemetry;
    lastUpdatedText?: string;
}

@Component({
    selector: 'app-root',
    standalone: true,
    imports: [MapComponent, AsyncPipe, DecimalPipe, DatePipe],
    templateUrl: './app.component.html',
    styleUrl: './app.component.css'
})
export class AppComponent implements OnInit {
    title = 'frontend';

    // State Subject for all known vehicles from the DB
    private allVehiclesSubject = new BehaviorSubject<Vehicle[]>([]);

    // The merged observable combining static DB vehicles with live SignalR telemetry
    fleetData$!: Observable<FleetVehicle[]>;

    selectedTruckId: string | null = null;
    previousTrips: Trip[] = [];
    selectedTripId: string | null = null;

    constructor(
        private telemetryService: TelemetryService,
        private historyService: HistoryService
    ) { }

    ngOnInit(): void {
        // 1. Fetch the static list of all vehicles from the DB
        this.historyService.getAllVehicles().subscribe({
            next: (vehicles) => this.allVehiclesSubject.next(vehicles),
            error: (err) => console.error('Failed to load vehicles from DB', err)
        });

        // 2. Combine the static DB list with the live SignalR stream
        this.fleetData$ = combineLatest([
            this.allVehiclesSubject.asObservable(),
            this.telemetryService.getTelemetryUpdates(),
            timer(0, 1000)
        ]).pipe(
            map(([allVehicles, activeTelemetry, _]) => {
                // Create a lookup map for faster telemetry matching
                const telemetryMap = new Map(activeTelemetry.map(t => [t.id, t]));
                const now = Date.now();

                return allVehicles.map(vehicle => {
                    const latestTelemetry = telemetryMap.get(vehicle.id);
                    let isOnline = false;
                    let lastUpdatedText = '';

                    if (latestTelemetry) {
                        const age = now - latestTelemetry.timestamp;
                        isOnline = age < 10000;
                        lastUpdatedText = this.formatTimeAgo(age);
                    }

                    return {
                        ...vehicle,
                        isOnline,
                        telemetry: latestTelemetry,
                        lastUpdatedText
                    };
                });
            })
        );
    }

    private formatTimeAgo(ms: number): string {
        const seconds = Math.floor(ms / 1000);
        if (seconds < 1) return 'just now';
        if (seconds < 60) return `${seconds}s ago`;
        const minutes = Math.floor(seconds / 60);
        return `${minutes}m ago`;
    }

    toggleTruck(id: string) {
        if (this.selectedTruckId === id) {
            this.selectedTruckId = null;
            this.previousTrips = [];
            this.selectedTripId = null;
        } else {
            this.selectedTruckId = id;
            this.historyService.getVehicleTrips(id).subscribe({
                next: (trips) => {
                    this.previousTrips = trips;
                    if (trips.length > 0) {
                        this.selectedTripId = trips[0].id;
                    } else {
                        this.selectedTripId = null;
                    }
                },
                error: (err) => {
                    console.error('Error fetching trips:', err);
                    this.previousTrips = [];
                    this.selectedTripId = null;
                }
            });
        }
    }

    selectTrip(tripId: string) {
        this.selectedTripId = tripId;
    }

    get activeTrip(): Trip | undefined {
        return this.previousTrips.find(t => !t.endTime || t.endTime.startsWith('0001-01-01'));
    }

    get completedTrips(): Trip[] {
        return this.previousTrips.filter(t => t.endTime && !t.endTime.startsWith('0001-01-01'));
    }
}
