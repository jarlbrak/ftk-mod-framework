# Hearthveil live-validation inputs

Each JSON file is a lossless snapshot of a transient isolated-game session file
used by the evidence archive. The original gameplay-session snapshot was
recovered byte-for-byte from the completed archive metadata because a fresh
isolated game process overwrites `model-test-session.json`. Future preview
snapshots are copied before their process is stopped. These files contain only
model-test metadata, not game binaries, Unity assets, or extracted native data.
