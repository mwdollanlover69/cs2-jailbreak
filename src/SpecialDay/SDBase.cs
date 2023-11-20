// base lr class
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

public abstract class SDBase
{
    public abstract void setup();

    public abstract void start();

    public abstract void end();

    public void setup_common()
    {
        // no damage before start
        restrict_damage = true;

        // revive all dead players


        state = SDState.INACTIVE;
        setup();
    }

    public void start_common()
    {
        restrict_damage = false;

        state = SDState.ACTIVE;
        start();
    }

    public void end_common()
    {
        state = SDState.INACTIVE;
        end();
    }

    public abstract void setup_player(CCSPlayerController player);

    public void setup_players()
    {
        foreach(CCSPlayerController player in Utilities.GetPlayers())
        {
            if(player.is_valid())
            {
                setup_player(player);
            }
        }       
    }

    public void announce(String str)
    {
        Lib.announce(SpecialDay.SPECIALDAY_PREFIX,str);
    }

    public enum SDState
    {
        INACTIVE,
        STARTED,
        ACTIVE
    };

    public bool restrict_damage = false;
    public SDState state = SDState.INACTIVE;
}