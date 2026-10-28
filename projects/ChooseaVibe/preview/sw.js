/**
 * Choose a Vibe — Service Worker (vibe-cache-v1)
 * Standalone PWA Offline Cache
 */

'use strict';

const CACHE_NAME = 'vibe-cache-v1';

const PRECACHE_ASSETS = [
  './',
  './index.html',
  './styles.css',
  './app.js',
  './manifest.json',
  './icons/apple-touch-icon.png',
  './icons/icon-192.png',
  './icons/icon-512.png',
  './icons/favicon-32x32.png'
];

// 1. Install Event: Precaching static shell
self.addEventListener('install', (event) => {
  self.skipWaiting();
  event.waitUntil(
    caches.open(CACHE_NAME).then((cache) => {
      return cache.addAll(PRECACHE_ASSETS);
    }).catch((err) => {
      console.warn('[SW] Precaching warning:', err);
    })
  );
});

// 2. Activate Event: Purge old cache versions and claim immediate control
self.addEventListener('activate', (event) => {
  event.waitUntil(
    caches.keys().then((cacheNames) => {
      return Promise.all(
        cacheNames.map((existingCache) => {
          if (existingCache !== CACHE_NAME) {
            console.log('[SW] Removing old cache:', existingCache);
            return caches.delete(existingCache);
          }
        })
      );
    }).then(() => self.clients.claim())
  );
});

// 3. Fetch Event:
// - Navigation requests: Network-First with Cache fallback (ensures fresh HTML while supporting offline)
// - Static assets (CSS, JS, images, fonts): Cache-First with Network fallback & cache update
self.addEventListener('fetch', (event) => {
  const request = event.request;

  // Only handle GET requests
  if (request.method !== 'GET') {
    return;
  }

  // Handle Navigation (HTML pages)
  if (request.mode === 'navigate') {
    event.respondWith(
      fetch(request)
        .then((networkResponse) => {
          if (networkResponse && networkResponse.status === 200) {
            const responseToCache = networkResponse.clone();
            caches.open(CACHE_NAME).then((cache) => {
              cache.put(request, responseToCache);
            });
          }
          return networkResponse;
        })
        .catch(() => {
          return caches.match(request).then((cachedResponse) => {
            return cachedResponse || caches.match('./index.html');
          });
        })
    );
    return;
  }

  // Handle Static Assets (Cache-First)
  event.respondWith(
    caches.match(request).then((cachedResponse) => {
      if (cachedResponse) {
        return cachedResponse;
      }

      return fetch(request).then((networkResponse) => {
        // Cache successful responses for local / same-origin and pre-cached resources
        if (networkResponse && networkResponse.status === 200) {
          const responseToCache = networkResponse.clone();
          caches.open(CACHE_NAME).then((cache) => {
            cache.put(request, responseToCache);
          });
        }
        return networkResponse;
      }).catch((fetchErr) => {
        // If resource is an image and fails offline, could return fallback or let fail
        console.warn('[SW] Fetch failed for:', request.url, fetchErr);
        throw fetchErr;
      });
    })
  );
});
