export const CFG = {
  GRID_W:            400,
  GRID_H:            400,
  TICK_RATE:         16,          // physics ticks per second
  PLAYER_SPEED:      55,          // grid units per second
  TURN_SPEED:        3.5,         // radians per second
  TRAIL_SAMPLE_DIST: 0.6,         // min dist between trail points
  COLLISION_RADIUS:  0.45,        // grid units, trail hit detection
  BOT_COUNT:         9,
  TRAIL_LIMIT:       800,         // max trail points → forced retreat
  BOT_THINK_MS:      200,
  FILL_ANIM_MS:      350,
  KILL_FEED_MS:      4000,
  MINIMAP_SIZE:      150,
  MINIMAP_PAD:       12,
  COLORS: ['#e74c3c','#3498db','#2ecc71','#f39c12','#9b59b6',
           '#1abc9c','#e91e63','#ff5722','#00bcd4','#8bc34a'],
};
