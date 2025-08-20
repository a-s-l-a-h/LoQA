﻿# LoQA (Local Question Answer)

LoQA is a simple, offline, cross-platform AI chat application.

### Philosophy

The core philosophy of LoQA is to provide a straightforward, accessible chat experience that works seamlessly across both desktop and mobile devices.

*   **Local First:** All models and conversations are stored and processed on your device. No internet connection is required for chatting.
*   **Cross-Platform:** A single codebase delivers a native experience on both Windows and Android.
*   **Accessibility:** Designed to run efficiently on standard consumer hardware, from your PC to your phone.

### Built With

*   **.NET MAUI:** For the cross-platform user interface and application logic.
*   **EasyChatEngine:** A custom C++ wrapper that simplifies `llama.cpp` into a clean, task-based C API, making it easy to call from .NET using P/Invoke.
*   **llama.cpp:** As the core inference engine for running GGUF models efficiently.
---

### Demo

See LoQA in action on both Windows and Android.

<details>
  <summary><strong>Watch Demo on Windows</strong></summary>
  <br>
  <video src="https://github.com/user-attachments/assets/234e5126-021c-484d-bf7d-3721afef832a" controls="controls" muted="muted" autoplay="autoplay" loop="loop" style="max-width:100%;">







    Your browser does not support the video tag.
  </video>
</details>

<details>
  <summary><strong>Watch Demo on Android</strong></summary>
  <br>
  <video src="https://github.com/user-attachments/assets/cf4d82fd-8694-4eae-9acd-daf6dd6dab4c" controls="controls" muted="muted" autoplay="autoplay" loop="loop" style="max-width:100%;">
    Your browser does not support the video tag.
  </video>
</details>


---

### Features

*   **llama.cpp Powered:** Supports a wide range of GGUF models, giving you the flexibility to choose the size and capability you need.
*   **Offline:** Your chats and models are processed locally.
*   **Cross-Platform:** Works on Windows and Android.
*   **Model Management:** Easily add GGUF files, configure settings, and switch between models.
*   **Conversation History:** Chats are automatically saved to a local database.
*   **Parameter Control:** Adjust sampling parameters like temperature on the fly.
*   **Advanced Settings:** Customize model-specific parameters like context size (CTX) and GPU layers.

---

### Build Instructions

LoQA requires a native backend to function. You must build this component first.

#### Step 1: Build the Native Engine (`easychatengine`)

The core inference is handled by `easychatengine`, a C++ wrapper around llama.cpp.

1.  Clone the engine's repository:
    ```bash
    git clone https://github.com/a-s-l-a-h/easychatengine.git
    ```
2.  Follow the build instructions in **that repository's README** to compile the native libraries for your target platforms (Windows and Android).

#### Step 2: Place the Compiled Binaries

After building, copy the resulting library files into the correct folders within this LoQA project:

*   **For Windows (x64):**
    Copy all `.dll` files (`easychatengine.dll`, `llama.dll`, etc.) to:
    ```
    LoQA/Platforms/Windows/libs/x64/
    ```

*   **For Android (arm64-v8a):**
    Copy all `.so` files (`libeasychatengine.so`, `libllama.so`, etc.) to:
    ```
    LoQA/Platforms/Android/libs/arm64-v8a/
    ```

#### Step 3: Build the LoQA App

With the native libraries in place, you can now build the .NET MAUI application.

1.  **Prerequisites:**
    *   .NET 9 SDK or later.
    *   Visual Studio 2022 with the ".NET Multi-platform App UI development" workload.

2.  **Build:**
    *   Open `LoQA.sln` in Visual Studio.
    *   Select your target (e.g., "Windows Machine" or an Android device).
    *   Build and run the project.

---

### How to Use

1.  **Download a Model:** First, download a compatible GGUF model to your local device. You can find a wide variety of text-generation models on the [Hugging Face Hub](https://huggingface.co/models?pipeline_tag=text-generation&library=gguf&apps=llama.cpp&sort=trending).
2.  **Launch LoQA:** Start the application.
3.  **Add the Model:** Open the sidebar, navigate to the **Models** page, and click **+ Add Model**. Select the GGUF file you just downloaded from your local storage.
4.  **Load the Model:** Once the model appears in the list, click its **Load** button.
5.  **Start Chatting:** After the model loads successfully, click **+ New Chat** in the sidebar to begin your conversation.

---

### Project Structure Overview

*   **Services:** Contains the core application logic.
    *   `EasyChatEngine.cs`: The C# wrapper that calls the native llama.cpp backend.
    *   `EasyChatService.cs`: Manages the application state, model loading, and chat logic.
    *   `DatabaseService.cs`: Handles the local SQLite database for conversations and models.
*   **Views:** Contains the UI pages and controls (`ChatContentPage.xaml`, `ModelsPage.xaml`, etc.).
*   **Platforms:** Contains platform-specific code and the native libraries you placed during the build steps.
