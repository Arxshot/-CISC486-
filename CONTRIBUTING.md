# How we work

Never push straight to `main`. Every change goes through a pull request.

```sh
git switch main
git pull                          # get the latest main
git switch -c my-change           # make a branch
# ...work in Unity / VS Code...
git add -A
just check                        # fix anything it reports
git commit -m "Add player jump"
git push -u origin my-change      # then open the pull request on GitHub
```

Merge the pull request on GitHub once the check passes (green tick).

## Unity rules

- Commit each `.meta` file together with its asset.
- Move, rename and delete assets inside Unity, not in Finder or Explorer.
- Only one person edits a scene at a time. Tell your team before you start.
- Don't change the Unity version or packages without the team agreeing.
