# cisc486

A 2.5D game made with **Unity 6 LTS** and **C#**.

| Folder / file | What it is |
|---|---|
| `game/` | The Unity project (open this folder in Unity Hub) |
| `tools/RepoCheck.cs` | The checks behind `just check` |
| `justfile` | Shortcuts: `just install`, `just setup`, `just check`, `just format` |
| `CONTRIBUTING.md` | How we use branches and pull requests |

## Setup (once per computer)

**1. Install Git and just**

- **Windows:** `winget install --exact --id Git.Git` and `winget install --exact --id Casey.Just`, then close and reopen the terminal.
- **macOS:** install [Homebrew](https://brew.sh), then `brew install git just`.
- **Linux:** install `git`, `git-lfs`, [`just`](https://github.com/casey/just#installation), the [.NET 10 SDK](https://learn.microsoft.com/dotnet/core/install/linux) and [Unity Hub](https://docs.unity.com/en-us/hub/install-hub-linux) yourself, then skip to step 3.

**2. Download the project and install the rest**

```sh
git clone https://github.com/arxshot/cisc486.git
cd cisc486
just install
just setup
```

`just install` installs the .NET SDK and Unity Hub (on macOS it also installs Git LFS; on Windows, Git LFS comes with Git).

**3. Get a free Unity license.** Apply for the [Unity Student plan](https://unity.com/products/unity-student). You'll receive a key by email. In Unity Hub, sign in, then go to **Settings → Licenses → Add license → Activate with a serial number**. The Student plan is for your own computer; ask your instructor about lab machines.

**4. Open the project.** In Unity Hub, go to **Projects → Add → Add project from disk** and choose the `game` folder. If Hub says the Editor version isn't installed, pick **Install other Editor version** and install the version it shows. Everyone must use that exact version.

**5. Code editor.** Install [VS Code](https://code.visualstudio.com) and its **Unity** extension (by Microsoft). In Unity, open **Edit → Preferences → External Tools** (macOS: **Unity → Settings → External Tools**) and set **External Script Editor** to *Visual Studio Code*. Visual Studio or Rider work too.

## Making changes

See [CONTRIBUTING.md](CONTRIBUTING.md).

## Tests

Use Unity's **Test Runner** (**Window → General → Test Runner**).

## Creating the Unity project (instructor, once)

1. Do steps 1–3 above.
2. In Unity Hub, go to **Installs → Install Editor** and install the latest **Unity 6 LTS**.
3. Go to **Projects → New project**, choose the **Universal 3D** template, name it `game`, and set the location to this repository's folder.
4. Close Unity, then commit `game/` on a branch and merge it with a pull request (see CONTRIBUTING.md).




##Alpha version demo video

https://youtu.be/BcPK-7YVlsw?si=46zn4mBG6-4M_c7A


## License

[MIT](LICENSE)
