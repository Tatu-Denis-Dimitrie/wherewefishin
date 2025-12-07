import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { AuthService } from '../../services/auth.service';

@Component({
  selector: 'app-home',
  imports: [CommonModule],
  template: `
    <div class="home-container">
      <header>
        <h1>Where We Fishin'</h1>
        <div class="user-info">
          <span>Bine ai venit!</span>
          <button (click)="logout()" class="logout-btn">Deconectare</button>
        </div>
      </header>
      
      <main>
        <div class="welcome-section">
          <h2>Bine ai venit la aplicația de pescuit!</h2>
          <p>Aici vei putea găsi și împărtăși cele mai bune locuri de pescuit.</p>
        </div>
      </main>
    </div>
  `,
  styles: [`
    .home-container {
      min-height: 100vh;
      background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
    }

    header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      padding: 20px 40px;
      background: rgba(255, 255, 255, 0.1);
      backdrop-filter: blur(10px);
    }

    h1 {
      color: white;
      margin: 0;
      font-size: 28px;
    }

    .user-info {
      display: flex;
      align-items: center;
      gap: 20px;
      color: white;
    }

    .logout-btn {
      padding: 10px 20px;
      background: rgba(255, 255, 255, 0.2);
      border: 2px solid white;
      border-radius: 8px;
      color: white;
      cursor: pointer;
      font-weight: 600;
      transition: all 0.3s ease;
    }

    .logout-btn:hover {
      background: white;
      color: #667eea;
    }

    main {
      display: flex;
      justify-content: center;
      align-items: center;
      padding: 60px 20px;
    }

    .welcome-section {
      background: white;
      padding: 40px;
      border-radius: 16px;
      box-shadow: 0 20px 60px rgba(0, 0, 0, 0.3);
      max-width: 600px;
      text-align: center;
    }

    .welcome-section h2 {
      color: #667eea;
      margin-bottom: 20px;
    }

    .welcome-section p {
      color: #666;
      font-size: 18px;
      line-height: 1.6;
    }
  `]
})
export class Home implements OnInit {
  constructor(
    private authService: AuthService,
    private router: Router
  ) {}

  ngOnInit(): void {
    if (!this.authService.isLoggedIn()) {
      this.router.navigate(['/login']);
    }
  }

  logout(): void {
    this.authService.logout();
  }
}
