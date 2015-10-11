﻿using FSO.Client.Regulators;
using FSO.Server.Clients;
using FSO.Server.Protocol.CitySelector;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FSO.Client.Network
{
    public class Network
    {
        private CityConnectionRegulator CityRegulator;
        private LoginRegulator LoginRegulator;

        public Network(LoginRegulator loginReg, CityConnectionRegulator cityReg){
            this.CityRegulator = cityReg;
            this.LoginRegulator = loginReg;
        }

        public AriesClient CityClient
        {
            get
            {
                return CityRegulator.Client;
            }
        }

        public uint MyCharacter
        {
            get
            {
                return uint.Parse(CityRegulator.CurrentShard.AvatarID);
            }
        }

        public ShardStatusItem MyShard
        {
            get
            {
                return LoginRegulator.Shards.First(x => x.Name == CityRegulator.CurrentShard.ShardName);
            }
        }
    }
}