// Prevents additional console window on Windows in release
#![cfg_attr(not(debug_assertions), windows_subsystem = "windows")]

use tauri::Manager;

// Named Pipe command for connecting to the Windows service
#[tauri::command]
async fn connect_named_pipe() -> Result<String, String> {
    // TODO: Implement named pipe connection to \\.\pipe\CapFrameXPmdData
    Ok("Connected to CapFrameXPmdData".to_string())
}

// Named Pipe command for disconnecting
#[tauri::command]
async fn disconnect_named_pipe() -> Result<String, String> {
    // TODO: Implement named pipe disconnection
    Ok("Disconnected from CapFrameXPmdData".to_string())
}

fn main() {
    tauri::Builder::default()
        .plugin(tauri_plugin_shell::init())
        .invoke_handler(tauri::generate_handler![
            connect_named_pipe,
            disconnect_named_pipe
        ])
        .run(tauri::generate_context!())
        .expect("error while running tauri application");
}
