import { Component } from '@angular/core';
import { RouterModule } from '@angular/router';
import { SiteFooter } from '../site-footer/site-footer';

@Component({
  selector: 'app-auth-shell',
  imports: [RouterModule, SiteFooter],
  templateUrl: './auth-shell.html',
  styleUrl: './auth-shell.css'
})
export class AuthShell {}
