import { Component, inject, OnInit } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { SwUpdate } from '@angular/service-worker';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet],
  templateUrl: './app.html',
  styleUrl: './app.css'
})
export class App implements OnInit {
  private readonly swUpdate = inject(SwUpdate);

  ngOnInit(): void {
    if (!this.swUpdate.isEnabled) return;

    this.swUpdate.versionUpdates.subscribe(event => {
      if (event.type === 'VERSION_READY') {
        this.swUpdate.activateUpdate().then(() => window.location.reload());
      }
      if (event.type === 'VERSION_INSTALLATION_FAILED') {
        this.clearCachesAndReload();
      }
    });

    this.swUpdate.unrecoverable.subscribe(() => this.clearCachesAndReload());

    this.swUpdate.checkForUpdate().catch(() => {});

    // Verifică actualizări la fiecare 30 minute
    setInterval(() => this.swUpdate.checkForUpdate().catch(() => {}), 30 * 60 * 1000);
  }

  private clearCachesAndReload(): void {
    navigator.serviceWorker.getRegistrations().then(regs =>
      Promise.all(regs.map(r => r.unregister()))
    ).then(() =>
      caches.keys()
    ).then(keys =>
      Promise.all(keys.map(k => caches.delete(k)))
    ).then(() =>
      window.location.reload()
    );
  }
}
