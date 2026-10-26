using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Gui
{
    public partial class Form1 : Form
    {
        // =====================================================================
        //  SMALL ACCESSORS - the same code serves player 1 and player 2
        // =====================================================================
        private ref float PosX(int w) { if (w == 1) return ref p1X; return ref p2X; }
        private ref float PosY(int w) { if (w == 1) return ref p1Y; return ref p2Y; }
        private ref float VelX(int w) { if (w == 1) return ref p1VelX; return ref p2VelX; }
        private ref float VelY(int w) { if (w == 1) return ref p1VelY; return ref p2VelY; }
        private ref int Health(int w) { if (w == 1) return ref p1Health; return ref p2Health; }
        private ref int ActionOf(int w) { if (w == 1) return ref p1Action; return ref p2Action; }
        private ref int ActionTimerOf(int w) { if (w == 1) return ref p1ActionTimer; return ref p2ActionTimer; }
        private ref int ComboHits(int w) { if (w == 1) return ref p1ComboHits; return ref p2ComboHits; }
        private ref int ComboTimer(int w) { if (w == 1) return ref p1ComboTimer; return ref p2ComboTimer; }

        private Player SpriteOf(int w) { return w == 1 ? p1Sprite : p2Sprite; }
        private static int Other(int w) { return w == 1 ? 2 : 1; }
        private bool OnGround(int w) { return PosY(w) >= GroundLevel; }
        private int CharIndex(int w) { return w == 1 ? p1CharIndex : p2CharIndex; }
        private bool IsHeld(Keys key) { return heldKeys.Contains(key); }

        private string FighterDisplayName(int w)
        {
            string name = GameData.Roster[CharIndex(w)].Name;
            return (w == 2 && isVsCPU) ? "CPU " + cpuDifficulty.ToString().ToUpperInvariant() + " (" + name + ")" : name;
        }

        // =====================================================================
        //  MATCH AND ROUND FLOW
        // =====================================================================
        // Each fighter keeps their own age. Only the fighter who LOSES a round moves to the next stage;
        // the winner stays the same age. A drawn round changes nothing.
        private AgeStage p1Age = AgeStage.Youth, p2Age = AgeStage.Youth;

        private static AgeStage NextAge(AgeStage age)
        {
            return age == AgeStage.Youth ? AgeStage.Veteran : AgeStage.Elder;
        }

        // "FINAL ROUND" only when it is the decider (both fighters one win away); otherwise "ROUND n"
        private string RoundLabel()
        {
            bool decider = p1RoundWins >= GameConfig.RoundsToWin - 1 && p2RoundWins >= GameConfig.RoundsToWin - 1;
            return decider ? "FINAL ROUND" : $"ROUND {roundNumber}";
        }

        private void StartMatch()
        {
            p1Age = p2Age = AgeStage.Youth;
            roundNumber = 1;
            p1RoundWins = p2RoundWins = 0;
            p1LostRound = p2LostRound = false;
            matchOver = false;
            lastRoundWinner = 0;
            roundResultText = "";
            nextRoundText = "";
            matchResultText = "";
            StartRound();
        }

        /// <summary>
        /// Everyone starts as Youth. When a fighter loses a round they age one stage (Youth -> Veteran -> Elder);
        /// the winner of the round keeps their current age.
        /// </summary>
        private AgeStage AgeForRound(int w, int round)
        {
            return w == 1 ? p1Age : p2Age;
        }

        private void StartRound()
        {
            // Aging also recalibrates max HP and clears the shadow meter and dash/duck/block state
            p1Sprite.SetAge(AgeForRound(1, roundNumber));
            p2Sprite.SetAge(AgeForRound(2, roundNumber));

            p1Health = p1RedHealth = (int)Math.Round(p1Sprite.MaxHealth);
            p2Health = p2RedHealth = (int)Math.Round(p2Sprite.MaxHealth);

            p1X = 260; p2X = 660;
            p1Y = GroundLevel; p2Y = GroundLevel;
            p1VelX = 0; p2VelX = 0;
            p1VelY = 0; p2VelY = 0;
            p1Action = 0; p2Action = 0;
            p1ActionTimer = 0; p2ActionTimer = 0;
            p1ComboHits = 0; p2ComboHits = 0;
            p1ComboTimer = 0; p2ComboTimer = 0;

            for (int w = 1; w <= 2; w++)
            {
                pendingMove[w] = 0; pendingDelay[w] = 0; dueMove[w] = 0;
                chainIndex[w] = 0; chainTimer[w] = 0;
                dashTimer[w] = 0;
                lastTapKey[w] = Keys.None; lastTapTime[w] = 0;
            }
            facingRight[1] = true; facingRight[2] = false;

            cpuDecisionTimer = 0; cpuMoveDir = 0f; cpuBlockTimer = 0; cpuDuckTimer = 0;

            roundTimer = GameConfig.RoundSeconds;
            introCounter = 90;
            hitStopFreeze = 0;
            screenShake = 0;

            shadows.Clear();
            ResetHeroSkills();
            craters.Clear();
            shockwaves.Clear();
            particles.Clear();
            lightnings.Clear();
            trails.Clear();
            floatTexts.Clear();
            ResetHeat();

            currentState = GameState.FightIntro;
            Sfx.Play(880, 200);
        }

        private void EndRound()
        {
            shadows.Clear();
            ResetHeroSkills();
            pendingMove[1] = pendingMove[2] = 0;

            int winner = p1Health > p2Health ? 1 : (p2Health > p1Health ? 2 : 0);
            bool knockout = p1Health <= 0 || p2Health <= 0;

            if (winner == 0)
            {
                koHeadline = "DRAW";
            }
            else
            {
                koHeadline = knockout ? "K.O." : "TIME!";
                int loser = Other(winner);
                ActionOf(loser) = CombatRules.ActDefeated;
                ActionTimerOf(loser) = 60;

                if (winner == 1) { p1RoundWins++; p2LostRound = true; p2Age = NextAge(p2Age); }
                else { p2RoundWins++; p1LostRound = true; p1Age = NextAge(p1Age); }
            }
            if (p1Health <= 0 && p2Health <= 0) koHeadline = "DOUBLE K.O.";

            lastRoundWinner = winner;
            matchOver = p1RoundWins >= GameConfig.RoundsToWin || p2RoundWins >= GameConfig.RoundsToWin;

            roundResultText = winner == 0
                ? "ROUND DRAWN - REPLAY"
                : $"{FighterDisplayName(winner)} WINS ROUND {roundNumber}   ({p1RoundWins} - {p2RoundWins})";

            if (matchOver)
            {
                matchResultText = $"{FighterDisplayName(winner)} WINS THE MATCH!";
                nextRoundText = "";
            }
            else
            {
                // Tell the players who ages up before the next round starts
                int upcoming = winner == 0 ? roundNumber : roundNumber + 1;
                nextRoundText = $"NEXT ROUND   -   P1: {CombatRules.AgeName(AgeForRound(1, upcoming))}   |   P2: {CombatRules.AgeName(AgeForRound(2, upcoming))}";
            }

            currentState = GameState.KO;
            koCounter = GameConfig.KoDisplayTicks;
            Sfx.Play(150, 450);
        }

        private void UpdateRoundEnd()
        {
            if (matchOver) return;   // the match is finished: wait for [R]

            koCounter--;
            if (koCounter > 0) return;

            if (lastRoundWinner != 0) roundNumber++;
            StartRound();
        }

        // =====================================================================
        //  CPU
        // =====================================================================
        // How each difficulty plays. "1 in N" odds are rolled every game tick (60 per second), so a bigger N is rarer.
        private readonly struct CpuProfile
        {
            public readonly int DecisionMin, DecisionMax;   // ticks between decisions (smaller = reacts faster)
            public readonly int AttackRoll;                 // out of 10: chance to attack when it has nothing better to do
            public readonly int ReactRoll;                  // out of 10: chance to defend against an incoming attack
            public readonly int DuckPick;                   // out of 10: duck (instead of block) against a high attack
            public readonly int StringOdds;                 // 1 in N per tick to keep a combo string going
            public readonly int ShadowOdds;                 // 1 in N per tick to drop a Chrono-Shadow mid-attack (0 = never)
            public readonly int ApproachOdds;               // 1 in N: jump (or dash) while closing in
            public readonly bool DuckSweep;                 // answers its own duck with a down attack

            public CpuProfile(int decisionMin, int decisionMax, int attackRoll, int reactRoll, int duckPick,
                int stringOdds, int shadowOdds, int approachOdds, bool duckSweep)
            {
                DecisionMin = decisionMin; DecisionMax = decisionMax; AttackRoll = attackRoll; ReactRoll = reactRoll;
                DuckPick = duckPick; StringOdds = stringOdds; ShadowOdds = shadowOdds; ApproachOdds = approachOdds; DuckSweep = duckSweep;
            }
        }

        //                                                              decide    atk react duck string shadow approach sweep
        private static CpuProfile ProfileFor(CpuDifficulty difficulty)
        {
            return difficulty switch
            {
                CpuDifficulty.Easy => new CpuProfile(20, 36, 3, 1, 3, 30, 0, 40, false),
                CpuDifficulty.Hard => new CpuProfile(4, 9, 8, 8, 7, 4, 18, 8, true),
                _ => new CpuProfile(8, 18, 6, 4, 5, 6, 45, 12, true)   // Medium plays exactly like the old CPU
            };
        }

        private void UpdateCPUAI()
        {
            CpuProfile cp = ProfileFor(cpuDifficulty);

            if (cpuBlockTimer > 0) cpuBlockTimer--;
            if (cpuDuckTimer > 0) cpuDuckTimer--;

            int act = p2Action;
            if (act == CombatRules.ActHurt || act == CombatRules.ActWallSplat || act == CombatRules.ActDefeated)
            {
                cpuMoveDir = 0f;
                return;
            }

            float dist = Math.Abs(p2X - p1X);
            float toward = p1X < p2X ? -1f : 1f;

            // Mid-attack: sometimes keep the string going, sometimes leave a Chrono-Shadow behind
            if (CombatRules.IsAttack(act))
            {
                if (rng.Next(0, cp.StringOdds) == 0) TryAttack(2, CpuAttackButton());
                else if (cp.ShadowOdds > 0 && p2Sprite.ShadowMeter >= GameConfig.ShadowCost && rng.Next(0, cp.ShadowOdds) == 0) TryDropShadow(2);
                return;
            }

            // Halfway through a duck, answer with a low sweep
            if (cp.DuckSweep && cpuDuckTimer == 5 && dist < 170f)
            {
                TryAttack(2, rng.Next(0, 2));   // still ducking, so this is the down attack (Q or E)
                return;
            }

            cpuDecisionTimer--;
            if (cpuDecisionTimer > 0) return;
            cpuDecisionTimer = rng.Next(cp.DecisionMin, cp.DecisionMax);
            cpuMoveDir = 0f;

            // Minsan gagamit ang CPU ng hero skill (F/G/H katumbas)
            if (dist < 450f && rng.Next(0, 14 - cp.AttackRoll) == 0)
            {
                UseHeroSkill(2, rng.Next(0, 3));
                return;
            }

            bool p1Attacking = CombatRules.IsAttack(p1Action);

            if (dist > 200f)
            {
                cpuMoveDir = toward;
                int r = rng.Next(0, cp.ApproachOdds);
                if (r == 0) TryJump(2);
                else if (r == 1) TryDash(2, toward);
            }
            else if (p1Attacking && rng.Next(0, 10) < cp.ReactRoll)
            {
                // React to an incoming attack: duck under high ones, block the rest
                if (CombatRules.GetMove(p1Action).High && rng.Next(0, 10) < cp.DuckPick) cpuDuckTimer = 14;
                else cpuBlockTimer = 18;
            }
            else
            {
                int c = rng.Next(0, 10);
                if (c < cp.AttackRoll)
                {
                    TryAttack(2, CpuAttackButton());
                }
                else
                {
                    int d = rng.Next(0, 4);
                    if (d == 0) cpuDuckTimer = 10;
                    else if (d == 1) cpuBlockTimer = 20;
                    else cpuMoveDir = -toward;
                }
            }
        }

        // =====================================================================
        //  HELD INPUT: walking, ducking, blocking
        // =====================================================================
        private void ApplyHeldInputs()
        {
            // P1: A/D move, S duck, T block
            ApplyFighterInput(1, IsHeld(Keys.A), IsHeld(Keys.D), IsHeld(Keys.S), IsHeld(Keys.T));

            if (isVsCPU) ApplyFighterInput(2, cpuMoveDir < 0f, cpuMoveDir > 0f, cpuDuckTimer > 0, cpuBlockTimer > 0);
            else ApplyFighterInput(2, IsHeld(Keys.J), IsHeld(Keys.L), IsHeld(Keys.K), IsHeld(Keys.Y));   // P2: J/L move, K duck, Y block
        }

        private void ApplyFighterInput(int w, bool left, bool right, bool down, bool block)
        {
            Player sp = SpriteOf(w);
            bool free = ActionOf(w) == CombatRules.ActNone;
            bool grounded = OnGround(w);

            sp.IsDucking = free && grounded && down;
            sp.IsBlocking = free && grounded && block;

            if (!free || sp.IsDucking || sp.IsBlocking || dashTimer[w] > 0 || heroDashTimer[w] > 0) return;

            float speed = GameConfig.WalkSpeed * sp.GetSpeedMultiplier();
            if (left && !right) VelX(w) = -speed;
            else if (right && !left) VelX(w) = speed;
        }

        // =====================================================================
        //  ACTIONS: jump, dash, attack, shadow
        // =====================================================================
        private void TryJump(int w)
        {
            if (ActionOf(w) != CombatRules.ActNone || !OnGround(w) || SpriteOf(w).IsDucking) return;
            VelY(w) = -GameConfig.JumpSpeed;
            Sfx.Play(440, 60);
        }

        /// <summary>Double-tap detection: the second tap of the same direction inside the window is a dash.</summary>
        private void RegisterDirectionTap(int w, Keys key, float dir)
        {
            long now = Environment.TickCount64;
            if (lastTapKey[w] == key && now - lastTapTime[w] <= GameConfig.DashDoubleTapMs)
            {
                lastTapKey[w] = Keys.None;
                TryDash(w, dir);
            }
            else
            {
                lastTapKey[w] = key;
                lastTapTime[w] = now;
            }
        }

        /// <summary>
        /// Youth: dash on the ground and once more in the air (double-dash).
        /// Veteran: one ground dash, no air dash. Elder: no dash at all.
        /// </summary>
        private void TryDash(int w, float dir)
        {
            Player sp = SpriteOf(w);
            if (ActionOf(w) != CombatRules.ActNone) return;

            if (OnGround(w))
            {
                if (!sp.CanGroundDash) return;
            }
            else
            {
                if (sp.AirDashCount >= sp.MaxAirDashes) return;
                sp.AirDashCount++;
                VelY(w) = 0;   // an air dash hangs in the air for a moment
            }

            VelX(w) = dir * GameConfig.DashSpeed;
            dashTimer[w] = GameConfig.DashPhaseTicks;
            Sfx.Play(760, 50);
            SpawnShockwave(new PointF(PosX(w) + 54, PosY(w) + 170), 60, GameData.Roster[CharIndex(w)].AuraColor);
        }

        // Is the fighter holding the duck key right now? (the held key, not the animation, so S + attack pressed
        // in the same instant still counts as a down attack)
        private bool HoldingDown(int w)
        {
            if (w == 1) return IsHeld(Keys.S);
            return isVsCPU ? cpuDuckTimer > 0 : IsHeld(Keys.K);
        }

        // Is the fighter holding the block key right now?
        private bool HoldingBlock(int w)
        {
            if (w == 1) return IsHeld(Keys.T);
            return !isVsCPU && IsHeld(Keys.Y);
        }

        /// <summary>
        /// Attack key (Q / U) and kick key (E / O) of a human player.
        ///   Attack: mash it and the string alternates punch, cross, punch, cross...
        ///   Kick  : the ground kick. In the air or while ducking, the kick key does the same as the attack key.
        /// </summary>
        private void PlayerAttack(int w, bool kick)
        {
            bool airborne = !OnGround(w) || VelY(w) < 0f;
            if (kick)
            {
                TryAttack(w, (airborne || HoldingDown(w)) ? 1 : 2);
                return;
            }

            int hitsIn = chainTimer[w] > 0 ? chainIndex[w] : 0;
            int limit = CombatRules.ComboString(SpriteOf(w).CurrentAge).Length;
            if (hitsIn >= limit) hitsIn = 0;
            TryAttack(w, hitsIn % 2 == 1 ? 1 : 0);
        }

        /// <summary>
        /// The one skill key (R / P). Idle: hero skill 1, hold duck: hero skill 2, hold block: hero skill 3.
        /// In the middle of an attack it drops a Chrono-Shadow instead (that is the only time a shadow works).
        /// </summary>
        private void PlayerSkill(int w)
        {
            if (ActionOf(w) != CombatRules.ActNone)
            {
                TryDropShadow(w);
                return;
            }

            int slot = HoldingBlock(w) ? 2 : (HoldingDown(w) ? 1 : 0);
            UseHeroSkill(w, slot);
        }

        private int CpuAttackButton()
        {
            return rng.Next(0, (OnGround(2) && !HoldingDown(2)) ? 3 : 2);   // R (kick) only exists on the ground
        }

        /// <summary>
        /// Three attack buttons: button 0 = Q/U (punch), 1 = E/O (cross), 2 = R/P (kick).
        ///   On the ground  : chains into a string (Youth up to 5 hits, Veteran / Elder up to 3)
        ///   Holding duck   : Q or E is the down attack
        ///   In the air     : Q or E is the jump attack (W + Q/E)
        /// </summary>
        private void TryAttack(int w, int button)
        {
            int action = ActionOf(w);
            if (action == CombatRules.ActHurt || action == CombatRules.ActWallSplat || action == CombatRules.ActDefeated) return;

            Player sp = SpriteOf(w);
            bool airborne = !OnGround(w) || VelY(w) < 0f;   // VelY < 0: the jump key was pressed this very tick
            int move;

            if (airborne)
            {
                if (button == 2 || action != CombatRules.ActNone) return;
                move = CombatRules.ActJumpAttack;
                chainIndex[w] = 0;
            }
            else if (HoldingDown(w))
            {
                if (button == 2 || action != CombatRules.ActNone) return;
                move = CombatRules.ActDownAttack;
                chainIndex[w] = 0;
            }
            else
            {
                int limit = CombatRules.ComboString(sp.CurrentAge).Length;   // 5 for Youth, 3 for Veteran and Elder
                int hitsIn;

                if (action == CombatRules.ActNone)
                {
                    hitsIn = chainTimer[w] > 0 ? chainIndex[w] : 0;
                }
                else if (CombatRules.IsAttack(action) && chainTimer[w] > 0 && ActionTimerOf(w) <= CombatRules.GetMove(action).CancelAt)
                {
                    hitsIn = chainIndex[w];   // the current hit is far enough along to chain into the next one
                }
                else
                {
                    return;
                }

                if (hitsIn >= limit)
                {
                    if (action != CombatRules.ActNone) return;   // the string is full: it ends here
                    hitsIn = 0;                                  // ...and a new one can start once the last hit is over
                }

                move = button == 0 ? CombatRules.ActPunch1 : (button == 1 ? CombatRules.ActPunch2 : CombatRules.ActKick);
                chainIndex[w] = hitsIn + 1;
            }

            StartMove(w, move);
        }

        private void StartMove(int w, int moveAction)
        {
            MoveInfo m = CombatRules.GetMove(moveAction);

            ActionOf(w) = moveAction;
            ActionTimerOf(w) = m.ActionFrames;
            pendingMove[w] = moveAction;
            pendingDelay[w] = m.HitDelay;
            chainTimer[w] = m.ActionFrames + 12;

            float dir = facingRight[w] ? 1f : -1f;
            VelX(w) += dir * m.Lunge * SpriteOf(w).GetSpeedMultiplier();
        }

        private void ResolveAttack(int attacker, int moveAction)
        {
            MoveInfo m = CombatRules.GetMove(moveAction);
            bool landed = ApplyHit(attacker, PosX(attacker), PosY(attacker), facingRight[attacker], SpriteOf(attacker).CurrentAge, m, false);
            if (!landed) Sfx.Play(280, 50);
        }

        /// <summary>
        /// Drops a Chrono-Shadow: a frozen, glowing copy of the attack you are in the middle of.
        /// Costs shadow meter, only works mid-attack, and one phantom per fighter at a time.
        /// </summary>
        private void TryDropShadow(int w)
        {
            Player sp = SpriteOf(w);
            int action = ActionOf(w);
            if (!CombatRules.IsAttack(action)) return;

            foreach (ChronoShadow existing in shadows)
            {
                if (existing.OwnerPlayer == w) return;
            }

            if (sp.ShadowMeter < GameConfig.ShadowCost)
            {
                Sfx.Play(180, 80);
                return;
            }

            sp.ShadowMeter -= GameConfig.ShadowCost;

            string animation = CombatRules.AnimationFor(action);
            shadows.Add(new ChronoShadow
            {
                OwnerPlayer = w,
                X = PosX(w),
                Y = PosY(w),
                FaceRight = facingRight[w],
                FighterIndex = CharIndex(w),
                Action = action,
                Animation = animation,
                Frame = sp.CurrentAnimation == animation ? sp.Frame : 0,
                Age = sp.CurrentAge
            });

            SpawnShockwave(new PointF(PosX(w) + 54, PosY(w) + 120), 100, Color.DeepSkyBlue);
            SpawnImpactParticles(PosX(w) + 54, PosY(w) + 100, 14, Color.DeepSkyBlue);
            Sfx.ShadowDrop();
        }

        // =====================================================================
        //  HERO SKILLS (P1: R     P2: P   -  skill 1 alone, skill 2 with duck held, skill 3 with block held)
        //  Magkakaiba ang skills ng bawat character, ayon sa kit na nakatalaga sa kanya.
        // =====================================================================
        private class HeroProjectile
        {
            public int Owner;
            public float X, Y, Vx;
            public int Damage, Radius = 12, Life = 140;
            public Color Col;
            public bool Pierce, Homing, Explosive, HitDone;
        }

        private readonly List<HeroProjectile> heroProjectiles = new List<HeroProjectile>();
        private readonly int[,] heroCd = new int[3, 3];          // [player, slot 0..2] cooldown sa ticks
        private readonly int[,] heroCdMax = new int[3, 3];       // buong cooldown ng huling gamit (para sa cooldown bar sa HUD)
        private readonly int[] heroBarrier = new int[3];
        private readonly int[] heroDashTimer = new int[3];
        private readonly float[] heroDashVel = new float[3];
        private readonly int[] heroDashDamage = new int[3];
        private readonly float[] heroDashLaunch = new float[3];
        private readonly bool[] heroDashHit = new bool[3];
        private readonly bool[] heroDashAir = new bool[3];
        private readonly int[] heroFlurry = new int[3];
        private readonly int[] heroFlurryTick = new int[3];

        // Isang kit bawat character, ayon sa pwesto niya sa roster (GameData.Roster).
        // Roster: 0 = Vyte, 1 = Aloy, 2 = Mokutin, 3 = Aerin
        // Kit 0 = Kazama style (electric, dash)   Kit 1 = Mishima style (laser, TELEPORT)
        // Kit 2 = Phoenix style (fire, barrier)   Kit 3 = Hworang style (kick beam, hawk kick)
        // Baguhin ang mga numero para ipagpalit, halimbawa { 2, 0, 3, 1 }
        private static readonly int[] HeroKitByRoster = { 0, 1, 2, 3 };

        private int HeroKit(int w)
        {
            return HeroKitByRoster[CharIndex(w) % HeroKitByRoster.Length];
        }

        private void ResetHeroSkills()
        {
            heroProjectiles.Clear();
            for (int w = 0; w < 3; w++)
            {
                for (int s = 0; s < 3; s++) heroCd[w, s] = 0;
                heroBarrier[w] = 0;
                heroDashTimer[w] = 0;
                heroDashHit[w] = false;
                heroFlurry[w] = 0;
            }
        }

        private void FireHero(int w, Color color, int damage, float speed, int radius,
                              bool pierce = false, bool homing = false, bool explosive = false, float yOffset = 0f)
        {
            float dir = facingRight[w] ? 1f : -1f;
            heroProjectiles.Add(new HeroProjectile
            {
                Owner = w,
                X = PosX(w) + 54f + (dir * 40f),
                Y = PosY(w) + 80f + yOffset,
                Vx = dir * speed,
                Damage = damage,
                Col = color,
                Radius = radius,
                Pierce = pierce,
                Homing = homing,
                Explosive = explosive
            });
            Sfx.Play(explosive ? 300 : 560, 80);
        }

        private void StartHeroDash(int w, float speed, float jumpY, int damage, int ticks)
        {
            float dir = facingRight[w] ? 1f : -1f;
            heroDashVel[w] = dir * speed;
            heroDashTimer[w] = ticks;
            heroDashDamage[w] = damage;
            heroDashLaunch[w] = jumpY < 0f ? -10f : 0f;
            heroDashHit[w] = false;
            heroDashAir[w] = jumpY < 0f;
            if (jumpY < 0f) VelY(w) = jumpY;
            Sfx.Play(760, 80);
        }

        private void HeroTeleport(int w)
        {
            int o = Other(w);
            Player sp = SpriteOf(w);
            float startX = PosX(w);
            float startY = PosY(w);

            // Puff sa pinanggalingan
            SpawnShockwave(new PointF(startX + 54, startY + 100), 110, Color.Purple);
            SpawnImpactParticles(startX + 54, startY + 100, 24, Color.MediumPurple);

            float away = PosX(o) >= PosX(w) ? 1f : -1f;
            int leftWall = 100, rightWall = this.ClientSize.Width - 180;
            float dest = PosX(o) + (away * 140f);                                       // sa likod ng kalaban
            if (dest < leftWall || dest > rightWall) dest = PosX(o) - (away * 140f);    // pader sa likod: sa harap na lang
            PosX(w) = Math.Clamp(dest, leftWall, rightWall);
            VelX(w) = 0;
            facingRight[w] = PosX(o) >= PosX(w);                                        // nakaharap sa kalaban pagdating

            // Mga after-image sa daan mula sa pinanggalingan hanggang sa pinuntahan
            const int steps = 6;
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                trails.Add(new GhostTrail
                {
                    X = startX + ((PosX(w) - startX) * t),
                    Y = startY + ((PosY(w) - startY) * t),
                    Color = i == steps ? Color.MediumVioletRed : Color.Purple,
                    Alpha = 255 - (i * 12),
                    Animation = sp.CurrentAnimation,
                    Frame = sp.Frame,
                    FaceRight = facingRight[w],
                    Fighter = CharIndex(w),
                    Age = sp.CurrentAge
                });
            }

            // Pagdating
            SpawnShockwave(new PointF(PosX(w) + 54, PosY(w) + 100), 130, Color.MediumPurple);
            SpawnImpactParticles(PosX(w) + 54, PosY(w) + 100, 30, Color.MediumVioletRed);
            CreateLightningBurst(PosX(w) + 54, PosY(w) + 90, Color.MediumPurple);
            Sfx.Play(980, 90);
            screenShake = 8;
        }

        private void UseHeroSkill(int w, int slot)
        {
            if (ActionOf(w) != CombatRules.ActNone) return;     // hindi habang inaatake o natamaan
            if (heroCd[w, slot] > 0) { Sfx.Play(180, 60); return; }

            // The ultimate (skill 3) needs a full HEAT bar and empties it
            if (slot == 2)
            {
                if (HeatOf(w) < MaxHeat)
                {
                    if (!(w == 2 && isVsCPU)) AddFloatText(PosX(w) + 54, PosY(w) - 10, "NEED FULL HEAT!", Color.Red);
                    Sfx.Play(180, 60);
                    return;
                }
                HeatOf(w) = 0;
            }

            switch (HeroKit(w))
            {
                case 0: // KAZAMA
                    if (slot == 0) { FireHero(w, Color.DeepSkyBlue, 7, 14f, 11); heroCd[w, 0] = 35; }
                    else if (slot == 1) { StartHeroDash(w, 24f, 0f, 12, 14); heroCd[w, 1] = 120; }       // Electric Fist
                    else { FireHero(w, Color.Cyan, 20, 12f, 24, pierce: true, explosive: true); heroCd[w, 2] = 420; }
                    break;

                case 1: // MISHIMA
                    if (slot == 0) { FireHero(w, Color.Purple, 8, 17f, 10, pierce: true); heroCd[w, 0] = 40; }   // laser na tumatagos
                    else if (slot == 1) { HeroTeleport(w); heroCd[w, 1] = 150; }                                // Shadow Step
                    else { FireHero(w, Color.MediumPurple, 24, 11f, 26, explosive: true); heroCd[w, 2] = 450; }
                    break;

                case 2: // PHOENIX
                    if (slot == 0) { FireHero(w, Color.OrangeRed, 8, 10f, 13, homing: true); heroCd[w, 0] = 45; }   // tumutudlong na fireball
                    else if (slot == 1)
                    {
                        heroBarrier[w] = 120;                                                                         // barrier 2 segundo
                        SpawnShockwave(new PointF(PosX(w) + 54, PosY(w) + 100), 110, Color.Cyan);
                        Sfx.Play(480, 90);
                        heroCd[w, 1] = 300;
                    }
                    else
                    {
                        // Infernal Phoenix Blast: nova sa paligid
                        int o = Other(w);
                        SpawnShockwave(new PointF(PosX(w) + 54, PosY(w) + 100), 240, Color.Red);
                        SpawnImpactParticles(PosX(w) + 54, PosY(w) + 100, 36, Color.OrangeRed);
                        screenShake = 16; hitStopFreeze = 6; Sfx.Play(300, 150);
                        if (Math.Abs(PosX(o) - PosX(w)) < 240f && Math.Abs(PosY(o) - PosY(w)) < 150f)
                            HeroHit(w, o, PosX(w), 24, 16f, -12f, 26, Color.Red, true);
                        heroCd[w, 2] = 480;
                    }
                    break;

                default: // HWORANG
                    if (slot == 0) { FireHero(w, Color.LawnGreen, 7, 19f, 10); heroCd[w, 0] = 30; }   // kick beam
                    else if (slot == 1) { StartHeroDash(w, 14f, -26f, 14, 45); heroCd[w, 1] = 150; }  // Hunting Hawk Kick
                    else { heroFlurry[w] = 6; heroFlurryTick[w] = 0; heroCd[w, 2] = 420; }            // Rapid Kick Flurry
                    break;
            }

            if (heroCd[w, slot] > 0) heroCdMax[w, slot] = heroCd[w, slot];
            if (slot == 2) heroCd[w, 2] = 0;   // the ultimate is gated by Heat, not by a cooldown

            AddFloatText(PosX(w) + 54, PosY(w) - 20, HeroSkillNames[HeroKit(w)][slot].ToUpperInvariant() + "!", GameData.Roster[CharIndex(w)].AuraColor);
        }

        // Isang hit ng hero skill. Kasama ang barrier, block, hyper armor.
        private bool HeroHit(int attacker, int defender, float sourceX, int damage, float knockback, float launch, int stun, Color fx, bool heavy)
        {
            if (ActionOf(defender) == CombatRules.ActDefeated) return false;

            Player def = SpriteOf(defender);
            float dir = PosX(defender) >= sourceX ? 1f : -1f;
            float defFront = facingRight[defender] ? 1f : -1f;
            bool fromFront = (sourceX - PosX(defender)) * defFront >= 0f;
            bool barrier = heroBarrier[defender] > 0;
            bool blocked = barrier || (def.IsBlocking && OnGround(defender) && fromFront);
            PointF hitP = new PointF(PosX(defender) + 54f, PosY(defender) + 90f);

            AddFloatText(hitP.X, hitP.Y - 40, blocked ? (barrier ? "BARRIER!" : "BLOCK") : "-" + damage, blocked ? (barrier ? Color.Cyan : Color.LightGray) : (heavy ? Color.Gold : Color.White));
            AddHeat(attacker, blocked ? 2 : 6);
            AddHeat(defender, blocked ? 1 : 4);

            if (blocked)
            {
                int chip = barrier ? 0 : CombatRules.ChipDamage(damage, SpriteOf(attacker).CurrentAge);
                Health(defender) = Math.Max(0, Health(defender) - chip);
                VelX(defender) += dir * knockback * 0.4f;
                SpawnShockwave(hitP, 70, barrier ? Color.Cyan : Color.LightGray);
                SpawnImpactParticles(hitP.X, hitP.Y, 8, Color.LightGray);
                hitStopFreeze = 2; screenShake = 3; Sfx.Play(220, 70);
                return true;
            }

            Health(defender) = Math.Max(0, Health(defender) - damage);
            if (!def.HasHyperArmor(ActionOf(defender)))
            {
                ActionOf(defender) = CombatRules.ActHurt;
                ActionTimerOf(defender) = stun;
                VelX(defender) = dir * knockback;
                if (launch < 0f) VelY(defender) = launch;
            }

            ComboHits(attacker)++;
            ComboTimer(attacker) = 45;
            hitStopFreeze = heavy ? 8 : 3;
            screenShake = heavy ? 12 : 6;
            SpawnShockwave(hitP, heavy ? 130 : 80, fx);
            SpawnImpactParticles(hitP.X, hitP.Y, heavy ? 26 : 14, fx);
            CreateLightningBurst(hitP.X, hitP.Y, Color.Gold);
            Sfx.Play(heavy ? 900 : 520, 100);
            return true;
        }

        // Tinatawag kada tick sa UpdateBattlePhysics
        private void UpdateHeroSkills()
        {
            for (int w = 1; w <= 2; w++)
            {
                int o = Other(w);
                for (int s = 0; s < 3; s++) if (heroCd[w, s] > 0) heroCd[w, s]--;
                if (heroBarrier[w] > 0) heroBarrier[w]--;

                // Forward strike (Electric Fist / Hunting Hawk)
                if (heroDashTimer[w] > 0)
                {
                    int act = ActionOf(w);
                    if (act == CombatRules.ActHurt || act == CombatRules.ActWallSplat || act == CombatRules.ActDefeated)
                    {
                        heroDashTimer[w] = 0;   // natamaan habang nagda-dash: putol ang skill
                    }
                    else
                    {
                        heroDashTimer[w]--;
                        VelX(w) = heroDashVel[w];
                        bool close = Math.Abs(PosX(w) - PosX(o)) < 85f && Math.Abs(PosY(w) - PosY(o)) < 110f;
                        if (!heroDashHit[w] && close)
                        {
                            heroDashHit[w] = true;
                            HeroHit(w, o, PosX(w), heroDashDamage[w], 14f, heroDashLaunch[w], 20, GameData.Roster[CharIndex(w)].AuraColor, true);
                            heroDashTimer[w] = 0;
                            VelX(w) = 0;
                        }
                        else if (heroDashAir[w] && OnGround(w) && heroDashTimer[w] < 40) heroDashTimer[w] = 0;
                    }
                }

                // Rapid Kick Flurry
                if (heroFlurry[w] > 0 && --heroFlurryTick[w] <= 0)
                {
                    FireHero(w, Color.GreenYellow, 4, 16f, 8, yOffset: rng.Next(-30, 31));
                    heroFlurry[w]--;
                    heroFlurryTick[w] = 5;
                }
            }

            // Mga projectile
            for (int i = heroProjectiles.Count - 1; i >= 0; i--)
            {
                HeroProjectile p = heroProjectiles[i];
                int d = Other(p.Owner);

                if (p.Homing) p.Y += ((PosY(d) + 90f) - p.Y) * 0.1f;
                float prevX = p.X;
                p.X += p.Vx;
                p.Life--;

                float cx = PosX(d) + 54f;
                float halfW = 40f + p.Radius + (p.Explosive ? 25f : 0f);
                bool xHit = Math.Max(prevX, p.X) >= cx - halfW && Math.Min(prevX, p.X) <= cx + halfW;
                bool yHit = p.Y + p.Radius >= PosY(d) - 10 && p.Y - p.Radius <= PosY(d) + 190;

                if (!p.HitDone && xHit && yHit)
                {
                    HeroHit(p.Owner, d, p.X - p.Vx, p.Damage, p.Explosive ? 14f : 7f, 0f, p.Explosive ? 24 : 12, p.Col, p.Explosive);
                    p.HitDone = true;
                    if (!p.Pierce) { heroProjectiles.RemoveAt(i); continue; }
                }

                if (p.Life <= 0 || p.X < -200 || p.X > this.ClientSize.Width + 200) heroProjectiles.RemoveAt(i);
            }
        }

        // Tawagin sa Form1.Rendering.cs pagkatapos mag-draw ng mga fighter
        private void DrawHeroEffects(Graphics g)
        {
            foreach (HeroProjectile p in heroProjectiles)
            {
                int size = p.Radius * 2;
                using (SolidBrush b = new SolidBrush(p.Col))
                using (SolidBrush core = new SolidBrush(Color.White))
                using (Pen glow = new Pen(Color.FromArgb(180, p.Col), 3))
                {
                    g.FillEllipse(b, p.X - p.Radius, p.Y - p.Radius, size, size);
                    g.FillEllipse(core, p.X - (p.Radius / 2f), p.Y - (p.Radius / 2f), p.Radius, p.Radius);
                    g.DrawEllipse(glow, p.X - p.Radius - 3, p.Y - p.Radius - 3, size + 6, size + 6);
                }
            }

            for (int w = 1; w <= 2; w++)
            {
                if (heroBarrier[w] <= 0) continue;
                int a = Math.Min(200, heroBarrier[w] * 6);
                using (Pen pen = new Pen(Color.FromArgb(a, Color.Cyan), 4))
                using (SolidBrush fill = new SolidBrush(Color.FromArgb(a / 5, Color.Cyan)))
                {
                    g.FillEllipse(fill, PosX(w) - 10, PosY(w) + 10, 130, 175);
                    g.DrawEllipse(pen, PosX(w) - 10, PosY(w) + 10, 130, 175);
                }
            }
        }

        // =====================================================================
        //  HIT RESOLUTION
        // =====================================================================
        /// <summary>
        /// Applies one hit. Used both for a fighter's own attack and for a phantom's strike (fromShadow),
        /// which hits from the phantom's position and facing.
        /// </summary>
        private bool ApplyHit(int attacker, float ax, float ay, bool attackerFacesRight, AgeStage attackerAge, MoveInfo m, bool fromShadow)
        {
            int defender = Other(attacker);
            int defAction = ActionOf(defender);
            if (defAction == CombatRules.ActDefeated) return false;

            float dir = attackerFacesRight ? 1f : -1f;
            float front = (PosX(defender) - ax) * dir;   // positive = the target is in front of the attacker
            if (front < -25f || front > m.Reach || Math.Abs(PosY(defender) - ay) >= 100f) return false;

            Player def = SpriteOf(defender);

            // High attacks pass over a fighter who is ducking
            if (m.High && def.IsDucking) return false;

            // A block only covers the side the defender is facing, so an attack that arrives from behind
            // (a phantom after a cross-up) cannot be blocked.
            float defFront = facingRight[defender] ? 1f : -1f;
            bool fromFront = (ax - PosX(defender)) * defFront >= 0f;
            bool barrier = heroBarrier[defender] > 0;                                   // Phoenix barrier
            bool blocked = (def.IsBlocking && OnGround(defender) && fromFront) || barrier;

            int raw = CombatRules.RawDamage(m, attackerAge);
            int dealt = barrier ? 0 : (blocked ? CombatRules.ChipDamage(raw, attackerAge) : raw);
            Health(defender) = Math.Max(0, Health(defender) - dealt);

            // The shadow meter fills by dealing and by taking damage
            Player atk = SpriteOf(attacker);
            atk.ShadowMeter = Math.Min(GameConfig.MaxShadowMeter, atk.ShadowMeter + (dealt * GameConfig.ShadowGainDealt));
            def.ShadowMeter = Math.Min(GameConfig.MaxShadowMeter, def.ShadowMeter + (dealt * GameConfig.ShadowGainTaken));

            bool heavy = m.Damage >= 10;
            Color fxColor = fromShadow ? Color.DeepSkyBlue : GameData.Roster[CharIndex(attacker)].AuraColor;

            float hitX = PosX(defender) + (dir > 0 ? 30f : 70f);
            float hitY = PosY(defender) + (m.Launch < -10f ? 110f : 60f);
            PointF hitP = new PointF(hitX, hitY);

            // Damage number, and Heat for both fighters (dealing a hit fills more than taking one)
            AddFloatText(hitP.X, hitP.Y - 30, blocked ? "BLOCK" : "-" + dealt, blocked ? Color.LightGray : (heavy ? Color.Gold : Color.White));
            AddHeat(attacker, blocked ? 3 : (heavy ? 12 : 8));
            AddHeat(defender, blocked ? 2 : 5);

            if (blocked)
            {
                VelX(defender) += dir * m.Knockback * 0.5f;
                SpawnShockwave(hitP, 60, barrier ? Color.Cyan : Color.LightGray);
                SpawnImpactParticles(hitP.X, hitP.Y, 8, Color.LightGray);
                hitStopFreeze = 2;
                screenShake = 3;
                Sfx.Play(220, 70);
                return true;
            }

            // Elder's Hyper Armor: the hit still hurts, but it does not interrupt an attack
            bool hyperArmor = def.HasHyperArmor(defAction);
            if (hyperArmor)
            {
                SpawnShockwave(hitP, 70, Color.Gold);
            }
            else
            {
                ActionOf(defender) = CombatRules.ActHurt;
                ActionTimerOf(defender) = m.Stun;
                VelX(defender) = dir * m.Knockback;
                if (m.Launch < 0) VelY(defender) = m.Launch;
            }

            ComboHits(attacker)++;
            ComboTimer(attacker) = 45;

            hitStopFreeze = heavy ? 8 : 3;
            screenShake = heavy ? 12 : 6;

            SpawnShockwave(hitP, heavy ? 130 : 80, fxColor);
            SpawnImpactParticles(hitP.X, hitP.Y, heavy ? 26 : 18, fxColor);
            CreateLightningBurst(hitP.X, hitP.Y, fromShadow ? Color.DeepSkyBlue : Color.Gold);

            speedLineCenter = hitP;
            speedLineTimer = heavy ? 12 : 6;

            Sfx.Play(heavy ? 900 : 520, 100);
            return true;
        }

        // =====================================================================
        //  PHYSICS TICK
        // =====================================================================
        private void UpdateFacing()
        {
            // Fighters always turn to face each other, so jumping or dashing over the opponent swaps their sides
            facingRight[1] = p1X <= p2X;
            facingRight[2] = !facingRight[1];
        }

        private void MoveFighter(int w)
        {
            PosX(w) += VelX(w);
            VelX(w) *= 0.82f;

            if (dashTimer[w] > 0) dashTimer[w]--;

            if (Math.Abs(VelX(w)) > 12f || dashTimer[w] > 0)
            {
                Player sp = SpriteOf(w);
                trails.Add(new GhostTrail
                {
                    X = PosX(w),
                    Y = PosY(w),
                    Color = GameData.Roster[CharIndex(w)].AuraColor,
                    Alpha = 140,
                    Animation = sp.CurrentAnimation,
                    Frame = sp.Frame,
                    FaceRight = facingRight[w],
                    Fighter = CharIndex(w),
                    Age = sp.CurrentAge
                });
            }

            PosY(w) += VelY(w);
            if (PosY(w) < GroundLevel)
            {
                VelY(w) += Gravity;
            }
            else
            {
                if (VelY(w) > 12)
                {
                    craters.Add(new FloorCrater { X = PosX(w) + 45, Y = GroundLevel + 160, Life = 100 });
                    SpawnShockwave(new PointF(PosX(w) + 45, GroundLevel + 160), 100, Color.White);
                    screenShake = 10;
                }
                PosY(w) = GroundLevel;
                VelY(w) = 0;

                // A jump attack ends when the fighter lands
                if (ActionOf(w) == CombatRules.ActJumpAttack)
                {
                    ActionOf(w) = CombatRules.ActNone;
                    ActionTimerOf(w) = 0;
                    pendingMove[w] = 0;
                }
            }
        }

        // Keeps two grounded fighters from standing inside each other. Dashing or airborne fighters pass through.
        private void SeparateFighters(int leftWall, int rightWall)
        {
            if (!OnGround(1) || !OnGround(2)) return;
            if (dashTimer[1] > 0 || dashTimer[2] > 0) return;
            if (p1Action == CombatRules.ActDefeated || p2Action == CombatRules.ActDefeated) return;

            const float minGap = 70f;
            float dx = p2X - p1X;
            float gap = minGap - Math.Abs(dx);
            if (gap <= 0f) return;

            float sign = dx >= 0f ? 1f : -1f;
            p1X -= sign * gap / 2f;
            p2X += sign * gap / 2f;
            p1X = Math.Clamp(p1X, leftWall, rightWall);
            p2X = Math.Clamp(p2X, leftWall, rightWall);

            // Anything still overlapping (one fighter is against a wall) is pushed out of the fighter who is not
            float newDx = p2X - p1X;
            float rest = minGap - Math.Abs(newDx);
            if (rest > 0f)
            {
                float s = newDx >= 0f ? 1f : -1f;
                bool p1AtWall = p1X <= leftWall || p1X >= rightWall;
                if (p1AtWall) p2X = Math.Clamp(p2X + (s * rest), leftWall, rightWall);
                else p1X = Math.Clamp(p1X - (s * rest), leftWall, rightWall);
            }
        }

        private void UpdateBattlePhysics()
        {
            if (p1RedHealth > p1Health) p1RedHealth--;
            if (p2RedHealth > p2Health) p2RedHealth--;

            ApplyHeldInputs();
            UpdateFacing();

            for (int w = 1; w <= 2; w++) MoveFighter(w);

            int leftWall = 80;
            int rightWall = this.ClientSize.Width - 160;

            for (int w = 1; w <= 2; w++)
            {
                if (PosX(w) <= leftWall)
                {
                    PosX(w) = leftWall;
                    if (ActionOf(w) == CombatRules.ActHurt && VelX(w) < -3f) TriggerWallSplat(w, true);
                }
                else if (PosX(w) >= rightWall)
                {
                    PosX(w) = rightWall;
                    if (ActionOf(w) == CombatRules.ActHurt && VelX(w) > 3f) TriggerWallSplat(w, false);
                }
            }

            SeparateFighters(leftWall, rightWall);
            UpdateFacing();

            // Timers, then attacks that reached their hit frame
            for (int w = 1; w <= 2; w++)
            {
                if (ActionTimerOf(w) > 0)
                {
                    ActionTimerOf(w)--;
                    if (ActionTimerOf(w) == 0) ActionOf(w) = CombatRules.ActNone;
                }

                if (ComboTimer(w) > 0)
                {
                    ComboTimer(w)--;
                    if (ComboTimer(w) == 0) ComboHits(w) = 0;
                }

                if (chainTimer[w] > 0)
                {
                    chainTimer[w]--;
                    if (chainTimer[w] == 0) chainIndex[w] = 0;
                }

                dueMove[w] = 0;
                if (pendingMove[w] != 0)
                {
                    if (ActionOf(w) != pendingMove[w])
                    {
                        pendingMove[w] = 0;   // the attack was interrupted by a hit
                    }
                    else if (--pendingDelay[w] <= 0)
                    {
                        dueMove[w] = pendingMove[w];
                        pendingMove[w] = 0;
                    }
                }
            }

            // Both fighters' hits are collected first, so two attacks landing on the same tick trade fairly
            for (int w = 1; w <= 2; w++)
            {
                if (dueMove[w] != 0) ResolveAttack(w, dueMove[w]);
            }

            UpdateHeroSkills();
            UpdateShadows();

            if (screenShake > 0) screenShake--;

            // Camera Following has been removed. Forced static camera zoom.
            cameraZoom = 1.0f;
            targetZoom = 1.0f;

            if (p1Health <= 0 || p2Health <= 0 || roundTimer <= 0)
            {
                EndRound();
            }
        }

        // Counts the phantoms down and lets them strike when their 3 seconds are up
        private void UpdateShadows()
        {
            for (int i = shadows.Count - 1; i >= 0; i--)
            {
                ChronoShadow s = shadows[i];
                bool wasExecuted = s.Executed;
                s.Update(frameSeconds);   // real seconds, so a phantom wakes after exactly 3 s whatever the frame rate

                if (s.Executed)
                {
                    if (!wasExecuted)
                    {
                        s.ExecTicks = 0;
                        Sfx.ShadowAwaken();
                        SpawnShockwave(new PointF(s.X + 54, s.Y + 120), 120, Color.DeepSkyBlue);
                        SpawnImpactParticles(s.X + 54, s.Y + 100, 20, Color.DeepSkyBlue);
                    }

                    s.ExecTicks++;
                    MoveInfo m = CombatRules.GetMove(s.Action);
                    if (!s.HitDone && s.ExecTicks >= m.HitDelay)
                    {
                        s.HitDone = true;
                        ApplyHit(s.OwnerPlayer, s.X, s.Y, s.FaceRight, s.Age, m, true);
                    }
                }

                if (s.Finished) shadows.RemoveAt(i);
            }
        }

        private void TriggerWallSplat(int w, bool atLeftWall)
        {
            screenShake = 16;
            hitStopFreeze = 5;
            Sfx.Play(200, 220);

            ActionOf(w) = CombatRules.ActWallSplat;
            ActionTimerOf(w) = 18;
            VelX(w) = atLeftWall ? 14f : -14f;

            Color c = GameData.Roster[CharIndex(w)].AuraColor;
            SpawnShockwave(new PointF(PosX(w) + (atLeftWall ? 0f : 80f), PosY(w) + 90), 120, c);
            SpawnImpactParticles(PosX(w) + (atLeftWall ? 0f : 80f), PosY(w) + 90, 16, c);
        }

        // =====================================================================
        //  VISUAL EFFECTS
        // =====================================================================
        private void SpawnShockwave(PointF center, float maxR, Color c)
        {
            shockwaves.Add(new ShockwaveRing { Center = center, Radius = 15, MaxRadius = maxR, Color = c, Alpha = 220 });
        }

        private void CreateLightningBurst(float startX, float startY, Color color)
        {
            for (int i = 0; i < 4; i++)
            {
                var arc = new LightningArc { Color = color, Life = 7 };
                float curX = startX, curY = startY;
                arc.Points.Add(new PointF(curX, curY));

                for (int j = 0; j < 5; j++)
                {
                    curX += rng.Next(-35, 36);
                    curY += rng.Next(-35, 36);
                    arc.Points.Add(new PointF(curX, curY));
                }
                lightnings.Add(arc);
            }
        }

        private void SpawnImpactParticles(float x, float y, int count, Color color)
        {
            for (int i = 0; i < count; i++)
            {
                particles.Add(new Particle
                {
                    X = x,
                    Y = y,
                    Vx = (float)(rng.NextDouble() * 12 - 6),
                    Vy = (float)(rng.NextDouble() * 12 - 6),
                    Life = 16,
                    MaxLife = 16,
                    Color = color,
                    Size = rng.Next(5, 11)
                });
            }
        }

        private void UpdateVisualEffects()
        {
            UpdateFloatTexts();

            for (int i = shockwaves.Count - 1; i >= 0; i--)
            {
                shockwaves[i].Radius += 8f;
                shockwaves[i].Alpha -= 14;
                if (shockwaves[i].Alpha <= 0 || shockwaves[i].Radius >= shockwaves[i].MaxRadius) shockwaves.RemoveAt(i);
            }

            for (int i = craters.Count - 1; i >= 0; i--)
            {
                craters[i].Life--;
                if (craters[i].Life <= 0) craters.RemoveAt(i);
            }

            foreach (var m in ambientMotes)
            {
                m.X += m.Vx;
                m.Y += m.Vy;
                if (m.Y < 40)
                {
                    m.Y = 560;
                    m.X = rng.Next(-100, 1200);
                }
            }

            for (int i = trails.Count - 1; i >= 0; i--)
            {
                trails[i].Alpha -= 18;
                if (trails[i].Alpha <= 0) trails.RemoveAt(i);
            }

            for (int i = particles.Count - 1; i >= 0; i--)
            {
                particles[i].X += particles[i].Vx;
                particles[i].Y += particles[i].Vy;
                particles[i].Life--;
                if (particles[i].Life <= 0) particles.RemoveAt(i);
            }

            for (int i = lightnings.Count - 1; i >= 0; i--)
            {
                lightnings[i].Life--;
                if (lightnings[i].Life <= 0) lightnings.RemoveAt(i);
            }
        }
    }
}