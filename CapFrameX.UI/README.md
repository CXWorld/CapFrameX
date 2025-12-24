# CapFrameX.UI

Desktop UI for CapFrameX - Performance monitoring and frame capture application.

## Technology Stack

- **Angular 18+** with Signals for reactive state management
- **Tauri 2** for desktop application wrapper
- **TypeScript** for type safety
- **ngx-echarts** for advanced charting
- **uPlot** for high-performance real-time charts
- **RxJS** for reactive programming

## Architecture

```
src/
├── app/
│   ├── core/
│   │   ├── services/        # API and Named Pipe services
│   │   └── models/          # TypeScript interfaces
│   ├── features/            # Feature modules (lazy loaded)
│   └── shared/
│       └── components/      # Reusable components
├── assets/                  # Static assets
└── environments/            # Environment configs

src-tauri/
└── src/
    └── main.rs              # Tauri commands for Named Pipe communication
```

## Communication

- **REST API**: `http://localhost:1337/api` - Standard CRUD operations
- **Named Pipe**: `\\.\pipe\CapFrameXPmdData` - Real-time power measurement data

## Getting Started

### Prerequisites

- Node.js 20+
- Rust (for Tauri)
- npm or yarn

### Installation

```bash
# Install dependencies
npm install

# Run Angular development server
npm start

# Run Tauri development build
npm run tauri dev

# Build for production
npm run tauri build
```

## Development

### Running the app

1. Start the backend service ([CapFrameX.Service](../CapFrameX.Service))
2. Run `npm start` for Angular dev server
3. Or run `npm run tauri dev` for full Tauri app

### Services

- **ApiService**: HTTP client for REST API communication
- **NamedPipeService**: Real-time data streaming via Windows Named Pipes (using Tauri commands)

### State Management

- Angular Signals for local component state
- RxJS for async data streams
- Service-based state for shared data

## Building

```bash
# Build Angular app
npm run build

# Build Tauri desktop app
npm run tauri build
```

The built application will be in `src-tauri/target/release`.
