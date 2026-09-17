import { Injectable, inject } from "@angular/core";
import { HttpClient, HttpContext } from "@angular/common/http";
import { BACKGROUND_REQUEST } from "./request-activity";
import { Observable, defer, finalize, shareReplay } from "rxjs";
import { environment } from "../../environments/environment";

@Injectable({ providedIn: "root" })
export class ApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = environment.apiUrl;

  private readonly pending = new Map<string, Observable<unknown>>();

  get<T>(path: string, background = false): Observable<T> {
    const key = (localStorage.getItem("access_token") ?? "") + ":" + this.toUrl(path);
    return defer(() => {
      let request = this.pending.get(key);
      if (!request) {
        request = this.http.get<T>(this.toUrl(path), { context: new HttpContext().set(BACKGROUND_REQUEST, background) })
          .pipe(finalize(() => this.pending.delete(key)), shareReplay({ bufferSize: 1, refCount: true }));
        this.pending.set(key, request);
      }
      return request as Observable<T>;
    });
  }

  post<TResponse, TRequest>(path: string, body: TRequest): Observable<TResponse> {
    return this.http.post<TResponse>(this.toUrl(path), body);
  }

  put<TResponse, TRequest>(path: string, body: TRequest): Observable<TResponse> {
    return this.http.put<TResponse>(this.toUrl(path), body);
  }

  private toUrl(path: string): string {
    if (/^https?:\/\//i.test(path)) return path;
    return `${this.baseUrl}/${path.replace(/^\/+/, "")}`;
  }
}
