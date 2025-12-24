import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="app-container">
      <h1>CapFrameX</h1>
      <p>Performance Monitoring Application</p>
    </div>
  `,
  styles: [`
    .app-container {
      padding: 2rem;
      text-align: center;
    }

    h1 {
      font-size: 2.5rem;
      margin-bottom: 1rem;
      color: #61dafb;
    }
  `]
})
export class AppComponent {
  title = 'CapFrameX';
}
