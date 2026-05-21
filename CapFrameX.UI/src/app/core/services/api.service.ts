import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import {
  AppVersionDto,
  CapabilitiesResponse,
  CaptureStatusDto,
  RecordsListResponse,
  ServiceHealthDto
} from '../models/bridge.models';

@Injectable({
  providedIn: 'root'
})
export class ApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = 'http://localhost:1337/api';
  readonly eventsUrl = `${this.baseUrl}/events`;

  getHealth(): Observable<ServiceHealthDto> {
    return this.http.get<ServiceHealthDto>(`${this.baseUrl}/health`);
  }

  getVersion(): Observable<AppVersionDto> {
    return this.http.get<AppVersionDto>(`${this.baseUrl}/app/version`);
  }

  getCapabilities(): Observable<CapabilitiesResponse> {
    return this.http.get<CapabilitiesResponse>(`${this.baseUrl}/capabilities`);
  }

  getCaptureStatus(): Observable<CaptureStatusDto> {
    return this.http.get<CaptureStatusDto>(`${this.baseUrl}/capture/status`);
  }

  getRecords(): Observable<RecordsListResponse> {
    return this.http.get<RecordsListResponse>(`${this.baseUrl}/records`);
  }
}
