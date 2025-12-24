import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

@Injectable({
  providedIn: 'root'
})
export class ApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = 'http://localhost:1337/api';

  /**
   * Health check endpoint
   */
  getHealth(): Observable<any> {
    return this.http.get(`${this.baseUrl}/health`);
  }

  // TODO: Add additional API methods here
}
