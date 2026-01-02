# 🧭 Git Strategy

This document outlines the branching model and release workflow for the project.

---

## 🔀 Branching Model

|Branch        | Purpose
|------------- | ---------------------------------------------------------------
|main          | Stable, tagged release versions only
|develop       | Ongoing integration of features and fixes
|feature/*     | Isolated work on specific tasks, merged into develop when complete
|

---

## 🧱 Workflow

### 🛠 Feature Development

1. Create a feature branch from develop:

```bash
   git checkout -b feature/your-feature-name develop
```

2. Work and commit freely.

3. Merge into develop when complete:
```bash
   git checkout develop
   git merge feature/your-feature-name
   git branch -d feature/your-feature-name  # optional cleanup
```

### 🚧 Ongoing Integration

- develop accumulates all feature work.
- May contain multiple commits and merges.
- Not intended for release without squashing or cleanup.

### 🚀 Releasing to main

1. Ensure develop is ready for release.
2. Fast-forward main to match develop:

```bash
   git checkout main
   git merge --ff-only develop
```

### 3. Tag the release:

```bash
   git tag -a vX.Y.Z -m "Release vX.Y.Z"
   git push origin main --tags
```

---

## 🩹 Hotfixes & Cherry-Picks

For urgent fixes that should go directly to main:

```bash
   git checkout main
   git cherry-pick <commit-sha>
   git tag -a vX.Y.Z+1 -m "Hotfix: description"
   git push origin main --tags
```

---

## 🏷 Versioning Guidelines

Tag      | Meaning
-------- | -------------------------------
v0.0.0   | Initial baseline
v0.1.0   | First usable version
v0.1.1   | Minor fix or tweak
v1.0.0   | First stable release

---

## 📤 Git Push Options Summary

Command                          | Use Case
------------------------------- | ------------------------------------------------------------
git push                         | Push current branch to its upstream
git push origin <branch>         | Push specific branch to remote
git push --tags                  | Push all local tags to remote
git push --force                 | Overwrite remote history (use with caution)
git push --force-with-lease      | Safer force-push (only if remote hasn’t changed)
git push origin main --tags      | Push main and all tags
git merge --ff-only develop      | Merge only if fast-forward is possible (no merge commits)


This strategy is designed for clarity, control, and clean release history.
Adjust as needed for team size or CI/CD requirements.
