using Akka.Actor;
using System;
using System.Linq;

namespace ImageResizer.Actors
{
    public class CommanderActor : ReceiveActor, IWithUnboundedStash
    {
        public class ScheduleMessage { };

        public IStash Stash
        {
            get; set;
        }

        //private int _nbCoordinators;
        const int MAX_NB_COORDINATORS = 10;
        private ICancelable _publishTimer;

        public CommanderActor()
        {
            var self = Self;
            Context.System.Scheduler.ScheduleTellRepeatedly(TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(50),
                                self, new ScheduleMessage(), self);
            Accepting();
        }

        private void Accepting()
        {
            Receive<CoordinatorActor.ResizeImage>(message => {
                //create coordinator actor to process image conversion
                var coordinatorActor = Context.ActorOf(Props.Create(() => new CoordinatorActor()));
                coordinatorActor.Tell(message);
                
                //if we have reached the maximum number of concurrent coordinators, become Saturated
                if (Context.GetChildren().Count() == MAX_NB_COORDINATORS)
                {
                    Become(Saturated);
                }
            });

            //Receive<CoordinatorActor.CoordinatorDone>(message => {
            //    _nbCoordinators--;                
            //});

            Receive<ScheduleMessage>(message => {
                Console.WriteLine("{0} coordinators currently at work", Context.GetChildren().Count());
            });
        }

        private void Saturated()
        {
            Receive<CoordinatorActor.ResizeImage>(message => {
                Stash.Stash();
            });

            //Receive<CoordinatorActor.CoordinatorDone>(message => {
            //    _nbCoordinators--;
                
            //    Become(Accepting);
            //});

            Receive<ScheduleMessage>(message => {

                var nbCoordinators = Context.GetChildren().Count();

                if(nbCoordinators > 0)
                {
                    var actorType = Context.GetChildren().First().GetType();
                }

                Console.WriteLine("{0} coordinators currently at work", nbCoordinators);
                if(nbCoordinators < MAX_NB_COORDINATORS)
                {
                    Stash.UnstashAll();
                    Become(Accepting);
                }
            });
        }
    }
}
