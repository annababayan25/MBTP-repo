# Myrtle Beach Travel Park Admin Website
This repository houses the web-application for Myrtle Beach Travel Park. We are expanding on existing front-end and back-end infrastructure.

--- 

## Setup

### 1. Install Git

- Download Git from [git-scm.com] (https://git-scm.com/downloads) and install it to your corresponding OS.
- Verify installation by opening a terminal and running:

  ```bash
  git --version

### 2. Verify Git Integration in VS Code
- Open VS Code
- Open the terminal inside VS Code
- Run: `git --version`
- If the version number appears, Git is recognized by VS Code

### 3. Configure Git User Info
- git config --global user.name "Your name"
- git config --global user.email "Your email"

### 4. Initialize Git Info
- Open your project folder in VS Code
- Open the terminal and run: `git init`

### 6 (Optional). Use Git Features in VS Code
- Click the ""*Source Control*"" icon on the left sidebar
- Stage files, commit changes, and sync, with remote repositories from this panel.

### 7. Clone Git Repo to VS Code
- Open Terminal
- In terminal, type and enter: `git clone https://github.com/annababayan25/mbtp-repo.git` (you can always copy the HTTPS link via the Git repository by clicking on the green `Code` button).
- A project directory called mbtp_repo should automatically be created
- In terminal, enter: `cd mbtp_repo`
- Open the cloned repository in VS Code

#### Restore NuGet dependencies 
dotnet restore

#### Build the project
dotnet build

#### Run the project
dotnet run

--- 

## Basic Git Commands

### Pull the latest changes from the remote repo. 
- `git pull origin main`

### Check Repo Status - Which files are changed, stacked, or untracked
- `git status`

### Stage changes to be committed  - This tells Git to include file(s) in the next commit
- `git add filename` (just a specific file)
- `git add .` (all changed files)

### Save (commit) the staged changes to the Git repo with a message
- `git commit -m "ex: Here are my changes"`

### *Important*: Branching - This allows for parallel development and isolated experimentation without making any changes to the main repository. `Main` is considered a branch as well.
- To create branch (but does not switch to it): `git branch my-branch-name`
- To switch branch: `git checkout my-branch-name`
- To create AND switch to branch: `git checkout -b my-branch-name`
- To list all branches: `git branch`
- To delete a branch (warns if unmerged): `git branch -d my-branch-name`

  - To stage and commit your changes: `git add.` and `git commit -m "Add feature: some function"
  - Push to the remote repo (git): `git push origin my-branch-name`
  - Pull to the remote repo (git): `git push origin my-branch-name`


### View commit history 
- `git log`

## RISKY Git Commands - Proceed with caution

- `git push --force origin main`: This forcefully overwrites history on the remote branch. It can erase previous commits.
- `git clean -fd`: Deletes untracked files
- `git rebase -i`: Reqrites commit history. 
