﻿
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Entities;
using CounterStrikeSharp.API.Modules.Events;
using CounterStrikeSharp.API.Modules.Memory;
using CounterStrikeSharp.API.Modules.Menu;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API.Modules.Entities.Constants;
using CSTimer = CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Memory.DynamicFunctions;

// main plugin file, controls central hooking
// defers to warden, lr and sd
public class JailPlugin : BasePlugin
{
    // workaround to query global state!
    public static JailPlugin? global_ctx;

    // Global event settings, used to filter plugin activits
    // during warday and SD
    bool is_event_active = false;

    public static bool is_warden(CCSPlayerController? player)
    {
        if(global_ctx == null)
        {
            return false;
        }
     
        return warden.is_warden(player);
    }

    public static bool event_active()
    {
        if(global_ctx == null)
        {
            return false;
        }

        return global_ctx.is_event_active;
    }

    public static void start_event()
    {
        if(global_ctx != null)
        {
            global_ctx.is_event_active = true;
        }
    }

    public static void end_event()
    {
        if(global_ctx != null)
        {
            global_ctx.is_event_active = false;
        }
    }

    public override string ModuleName => "CS2 Jailbreak - destoer";

    public override string ModuleVersion => "0.0.1";

    public override void Load(bool hotReload)
    {
        if(Lib.is_windows())
        {
            Console.WriteLine("This plugin only works on linux - (OnTakeDamage and others are broken)");
            return;
        }

        global_ctx = this;

        register_commands();
        
        register_hook();

        register_listener();

        Console.WriteLine("Sucessfully started JB");
    }

    void register_listener()
    {
        RegisterListener<Listeners.OnEntitySpawned>(entity =>
        {
            lr.ent_created(entity);
            sd.ent_created(entity);
        });
    }

    void register_commands()
    {
        // reg warden comamnds
        AddCommand("w", "take warden", warden.take_warden_cmd);
        AddCommand("uw", "leave warden", warden.leave_warden_cmd);
        AddCommand("rw", "remove warden", warden.remove_warden_cmd);

        AddCommand("wub","warden : disable block",warden.wub_cmd);
        AddCommand("wb","warden : enable block",warden.wb_cmd);

        AddCommand("wd","warden : start warday",warden.warday_cmd);

        // reg lr commands
        AddCommand("lr","start an lr",lr.lr_cmd);
        AddCommand("cancel_lr","admin : cancel lr",lr.cancel_lr_cmd);
        AddCommand("lr_stats","list lr stats",lr.lr_stats.lr_stats_cmd);

        // reg sd commands
        AddCommand("sd","start a sd",sd.sd_cmd);
        AddCommand("sd_ff","start a ff sd",sd.sd_ff_cmd);
        AddCommand("cancel_sd","cancel an sd",sd.cancel_sd_cmd);

        // debug 
        if(Debug.enable)
        {
            AddCommand("nuke","debug : kill every player",Debug.nuke);
            AddCommand("force_open","debug : force open every door and vent",Debug.force_open_cmd);
            AddCommand("is_rebel","debug : print rebel state to console",warden.is_rebel_cmd);
            AddCommand("lr_debug","debug : start an lr without restriction",lr.lr_debug_cmd);
            AddCommand("is_blocked","debug : print block state",warden.block.is_blocked);
            AddCommand("test_laser","test laser",Debug.test_laser);
            AddCommand("test_strip","test weapon strip",Debug.test_strip_cmd);
        }
    }


    void register_hook()
    {
        RegisterEventHandler<EventRoundStart>(OnRoundStart);
        RegisterEventHandler<EventRoundEnd>(OnRoundEnd);
        RegisterEventHandler<EventWeaponFire>(OnWeaponFire);
        RegisterEventHandler<EventPlayerSpawn>(OnPlayerSpawn);
        RegisterEventHandler<EventPlayerDisconnect>(OnPlayerDisconnect);
        RegisterEventHandler<EventTeamchangePending>(OnSwitchTeam);
        RegisterEventHandler<EventMapTransition>(OnMapChange);
        RegisterEventHandler<EventPlayerDeath>(OnPlayerDeath);
        RegisterEventHandler<EventItemEquip>(OnItemEquip);
        RegisterEventHandler<EventGrenadeThrown>(OnGrenadeThrown);
        RegisterEventHandler<EventPlayerHurt>(OnPlayerHurt);
        VirtualFunctions.CBaseEntity_TakeDamageOldFunc.Hook(OnTakeDamage,HookMode.Pre);
        

        // TODO: need to hook weapon drop
    }

    public override void Unload(bool hotReload)
    {
        VirtualFunctions.CBaseEntity_TakeDamageOldFunc.Unhook(OnTakeDamage,HookMode.Pre);
    }

    HookResult OnGrenadeThrown(EventGrenadeThrown @event, GameEventInfo info)
    {
        CCSPlayerController? player = @event.Userid;

        if(player != null && player.is_valid())
        {
            lr.grenade_thrown(player);
            sd.grenade_thrown(player);
        }

        return HookResult.Continue;
    }
  

    HookResult OnItemEquip(EventItemEquip @event, GameEventInfo info)
    {
        CCSPlayerController? player = @event.Userid;

        if(player != null && player.is_valid())
        {
            lr.weapon_equip(player,@event.Item);
            sd.weapon_equip(player,@event.Item);
        }

        return HookResult.Continue;
    }

    HookResult OnPlayerHurt(EventPlayerHurt @event, GameEventInfo info)
    {
        CCSPlayerController? player = @event.Userid;
        CCSPlayerController? attacker = @event.Attacker;

        int damage = @event.DmgHealth;
        int health = @event.Health;
        int hitgroup = @event.Hitgroup;

        if(player != null && player.is_valid())
        {
            lr.player_hurt(player,attacker,damage,health,hitgroup);
            warden.player_hurt(player,attacker,damage,health);
            sd.player_hurt(player,attacker,damage,health,hitgroup);
        }

        return HookResult.Continue;
    }

    HookResult OnTakeDamage(DynamicHook handle)
    {
        CEntityInstance victim = handle.GetParam<CEntityInstance>(0);
        CTakeDamageInfo damage_info = handle.GetParam<CTakeDamageInfo>(1);

        CHandle<CBaseEntity> dealer = damage_info.Attacker;

        // get player and attacker
        CCSPlayerController? player = victim.player();
        CCSPlayerController? attacker = dealer.player();

        sd.take_damage(player,attacker,ref damage_info.Damage);
        lr.take_damage(player,attacker,ref damage_info.Damage);

        return HookResult.Continue;
    }

    HookResult OnMapChange(EventMapTransition @event, GameEventInfo info)
    {
        warden.map_start();

        return HookResult.Continue;
    }

    HookResult OnRoundStart(EventRoundStart @event, GameEventInfo info)
    {
        warden.round_start();
        lr.round_start();
        sd.round_start();

        return HookResult.Continue;
    }

    HookResult OnPlayerDeath(EventPlayerDeath @event, GameEventInfo info)
    {
        CCSPlayerController? player = @event.Userid;
        CCSPlayerController? killer = @event.Attacker;

        if(player != null && player.is_valid())
        {
            warden.death(player,killer);
            lr.death(player);
            sd.death(player,killer);
        }

        return HookResult.Continue;
    }

    HookResult OnPlayerSpawn(EventPlayerSpawn @event, GameEventInfo info)
    {
        CCSPlayerController? player = @event.Userid;

        if(player != null && player.is_valid())
        {
            if(Lib.is_windows())
            {
                warden.spawn(player);
            }
            
            else
            {
                AddTimer(0.5f,() => warden.spawn(player));
            }
        }

        return HookResult.Continue;
    }

    HookResult OnSwitchTeam(EventTeamchangePending @event, GameEventInfo info)
    {
        CCSPlayerController? player = @event.Userid;

        int new_team = @event.Toteam;

        if(player != null && player.is_valid())
        {
            warden.switch_team(player,new_team);
        }

        return HookResult.Continue;
    }

    HookResult OnPlayerDisconnect(EventPlayerDisconnect @event, GameEventInfo info)
    {
        CCSPlayerController? player = @event.Userid;

        if(player != null && player.is_valid())
        {
            warden.disconnect(player);
            lr.disconnect(player);
        }

        return HookResult.Continue;
    }

    HookResult OnRoundEnd(EventRoundEnd @event, GameEventInfo info)
    {
        warden.round_end();
        lr.round_end();
        sd.round_end();

        return HookResult.Continue;
    }

    HookResult OnWeaponFire(EventWeaponFire @event, GameEventInfo info)
    {
        // attempt to get player and weapon
        var player = @event.Userid;
        String name = @event.Weapon;

        warden.weapon_fire(player,name);
        lr.weapon_fire(player,name);

        return HookResult.Continue;
    }


    static Warden warden = new Warden();
    static LastRequest lr = new LastRequest();
    static SpecialDay sd = new SpecialDay();
}