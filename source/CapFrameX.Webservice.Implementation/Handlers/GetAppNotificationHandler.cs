using AutoMapper;
using CapFrameX.Webservice.Data.DTO;
using CapFrameX.Webservice.Data.Queries;
using CapFrameX.Webservice.Implementation.Services;
using MediatR;
using System.Threading;
using System.Threading.Tasks;

namespace CapFrameX.Webservice.Implementation.Handlers
{
    public class GetAppNotificationHandler: IRequestHandler<GetAppNotificationQuery, SqAppNotificationDataDTO>
    {
        private readonly SquidexService _squidexService;
        private readonly IMapper _mapper;

        public GetAppNotificationHandler(SquidexService squidexService, IMapper mapper)
        {
            _squidexService = squidexService;
            _mapper = mapper;
        }

        public async Task<SqAppNotificationDataDTO> Handle(GetAppNotificationQuery request, CancellationToken cancellationToken)
        {
            var notification = (await _squidexService.GetAppNotification())?.Data;
            return notification is null ? null : _mapper.Map<SqAppNotificationDataDTO>(notification);
        }
    }
}
