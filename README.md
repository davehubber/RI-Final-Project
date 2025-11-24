# 🤖 MARL Balloon-Popping Robot Competition

## Overview

This project utilizes **Unity** and the **ML-Agents** library to test a Multi-Agent Reinforcement Learning (MARL) competition. The environment consists of two opposing teams of robots fighting in a closed arena.

### The Game Rules
* **The Objective:** Completely eliminate the opposing team. A team wins when all robots on the opposing team have lost all their balloons.
* **The Agents:** Simple cylinder robots with linear and angular velocity control.
    * **Sensors:** "Simulated" Lidar (Ray Perception Sensor) to detect walls, enemies, and balloons.
    * **Equipment:** A **Spike** on the front and a row of **3 Balloons** on the back.
    * **Abilities:** A "Speed Boost" (short duration, fixed cooldown).
* **Combat Mechanics:**
    * **Popping:** To damage an enemy, a robot must drive its spike into an enemy's balloon.
    * **Elimination:** When a robot loses all 3 balloons, it enters a "Dead" state. It stops moving and remains in the arena as a static physical obstacle.
* **The Arena:**
    * Contains static walls.
    * **Restocking:** There are 2 zones where balloons spawn periodically. Robots can pick these up to replenish health (Max capacity: 3 balloons).
    * **Spawning:** Robots start at random positions at the beginning of every match.

---

## 🛠️ Setup Instructions

### 1. Unity Environment
1.  **Install Unity Editor:**
    * Version: **`6000.2.7f2`** (Install via Unity Hub).
2.  **Install ML-Agents:**
    * Open the project.
    * Go to `Window` > `Package Manager`.
    * Click the `+` icon in the top left.
    * Search for ML Agents and install it.

### 2. Python Environment
**Prerequisites:**
* Install **Python 3.10.11** (or another version of 3.10, if you can't find this one).

**Step-by-Step Setup:**

1.  **Create the Virtual Environment (Venv):**
    Open your terminal at the **root** of this project folder.

    * **Windows:**
        ```bash
        python -m venv venv
        ```
    * **Mac / Linux:**
        ```bash
        python3 -m venv venv
        ```

2.  **Activate the Environment:**

    * **Windows:**
        ```bash
        .\venv\Scripts\activate
        ```
    * **Mac / Linux:**
        ```bash
        source venv/bin/activate
        ```

3.  **Install Dependencies:**
    Once the environment is active (you should see `(venv)` in your terminal), run:
    ```bash
    pip install -r requirements.txt
    ```

---

## ✅ To-Do List
- [ ] Implement Collision Logic: Spike hits Balloon -> Balloon Pop (The visual is disabled, much cheaper than destroying and creating).
- [ ] Implement Death Logic: 0 Balloons -> Disable Agent script -> Robot becomes static obstacle.
- [ ] Create the Map: Walls, Floor.
- [ ] Implement Balloon Spawner logic (2 zone objects. Check if a balloon exists; if not, wait X seconds and instantiate one).
- [ ] Implement "Pickup" logic (Robot touches spawn balloon -> +1 Balloon, cannot go over max of 3).
- [ ] Make sure `RayPerceptionSensor` can distinguish between a Wall, an Enemy (Dead/Alive), and a Balloon (Tags: Wall, Enemy, Balloon, DeadBot).
- [ ] Configure Behavior Parameters component of the BattleBotAgent (Vector Observation, Actions etc.).
- [ ] Use `Heuristic` controls of the `BattleBotAgent` to test with the keyboard and make sure action logic (speed, turning, boost and collision) is working and beeing registered correctly.
- [ ] Define the Reward Function (e.g. Individual reward for popping, penalty for time, large team reward for winning).
- [ ] Create `MatchManager` script to define the teams and agent groups (probably will use SimpleMultiAgentGroup) and handle Game State (Tracks how many robots are alive on Team A vs Team B. When a team reaches 0 agents, it calls EndEpisode() on all agents and resets the map. Handles random spawn positions for the robots at the start of a match).
- [ ] Create `trainer_config.yaml` to configure all the RL parameters.
- [ ] Successfully run a training loop (`mlagents-learn`) and verify TensorBoard stats.
