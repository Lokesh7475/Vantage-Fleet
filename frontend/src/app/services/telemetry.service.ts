import { Injectable } from '@angular/core';
import { Observable, BehaviorSubject } from 'rxjs';
import * as signalR from '@microsoft/signalr';
import { environment } from '../../environments/environment';

export interface VehicleTelemetry {
    id: string;
    lat: number;
    lng: number;
    speed: number;
    heading: number; // 0-360 degrees
    timestamp: number;
}

interface BackendTelemetry {
    vehicleId: string;
    latitude: number;
    longitude: number;
    speed: number;
    heading: number;
}

@Injectable({
    providedIn: 'root'
})
export class TelemetryService {
    private hubConnection: signalR.HubConnection | undefined;
    private telemetryMap = new Map<string, VehicleTelemetry>();
    private telemetrySubject = new BehaviorSubject<VehicleTelemetry[]>([]);

    constructor() {
        this.startSignalRConnection();
    }

    private startSignalRConnection() {
        this.hubConnection = new signalR.HubConnectionBuilder()
            .withUrl(`${environment.stateServiceUrl}/hubs/telemetry`) // Backend StateService URL
            .withAutomaticReconnect()
            .build();

        this.hubConnection
            .start()
            .then(() => console.log('SignalR connection established with StateService'))
            .catch(err => console.error('Error while starting connection: ' + err));

        this.hubConnection.on('ReceiveTelemetry', (data: BackendTelemetry) => {
            // Map backend DTO to frontend interface
            const telemetry: VehicleTelemetry = {
                id: data.vehicleId,
                lat: data.latitude,
                lng: data.longitude,
                speed: data.speed,
                heading: data.heading,
                timestamp: Date.now()
            };

            // Update the map keeping latest state per vehicle
            this.telemetryMap.set(telemetry.id, telemetry);

            // Push updated array
            this.telemetrySubject.next(Array.from(this.telemetryMap.values()));
        });
    }

    getTelemetryUpdates(): Observable<VehicleTelemetry[]> {
        return this.telemetrySubject.asObservable();
    }
}
