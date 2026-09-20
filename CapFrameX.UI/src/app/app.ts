import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';

import { Rail } from './core/shell/rail';
import { StatusBar } from './core/shell/status-bar';

/**
 * The application shell: rail, workspace, status bar.
 *
 * The pattern every view follows is rail then context list then workspace, but only the rail and
 * the status bar are the shell's. The middle column belongs to the view, because what it lists -
 * records, processes, sensor groups, overlay profiles - is the view's own idea.
 */
@Component({
  selector: 'cx-root',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [Rail, RouterOutlet, StatusBar],
  template: `
    <div class="frame">
      <cx-rail />
      <main class="workspace">
        <router-outlet />
      </main>
    </div>
    <cx-status-bar />
  `,
  styles: `
    :host {
      display: flex;
      flex-direction: column;
      height: 100%;
    }

    .frame {
      display: flex;
      flex: 1;
      min-height: 0;
    }

    .workspace {
      display: flex;
      flex: 1;
      min-width: 0;
    }
  `,
})
export class App {}
