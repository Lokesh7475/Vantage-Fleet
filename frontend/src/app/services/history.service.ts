import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';

export interface Vehicle {
    id: string;
    description: string | null;
}

export interface Trip {
    id: string;
    vehicleId: string;
    startTime: string;
    endTime: string | null;
    startLatitude: number;
    startLongitude: number;
    endLatitude: number | null;
    endLongitude: number | null;
}

export interface RoutePoint {
    latitude: number;
    longitude: number;
    speed: number;
    timestamp: string;
}

@Injectable({
    providedIn: 'root'
})
export class HistoryService {
    private readonly baseUrl = `${environment.historicalServiceUrl}/api/history`;

    constructor(private http: HttpClient) { }

    getAllVehicles(): Observable<Vehicle[]> {
        return this.http.get<Vehicle[]>(`${this.baseUrl}/vehicles`);
    }

    getVehicleTrips(vehicleId: string): Observable<Trip[]> {
        return this.http.get<Trip[]>(`${this.baseUrl}/vehicle/${vehicleId}/trips`);
    }

    getTripRoute(tripId: string): Observable<RoutePoint[]> {
        return this.http.get<RoutePoint[]>(`${this.baseUrl}/trip/${tripId}/route`);
    }
}
