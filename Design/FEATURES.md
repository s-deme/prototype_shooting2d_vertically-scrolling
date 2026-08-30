# Product feature coverage

The current Unity implementation covers the core player loop (movement, shooting, focused movement, collision, lives, bombs, power, pickups, score, graze, chain and boss phases), three-stage progression, difficulty and shot-type selection, session flow (title, normal run, spell practice, pause, continue, retry, defeat, clear and name entry), and local product features (rankings, records, achievements, ghost replay paths, configurable audio, reduced motion, high contrast, text scaling, controls, touch controls and fullscreen).

Save data is intentionally device-local through PlayerPrefs, with a local backup and an offline score-submission queue. No account, network service or external asset is required to play.
