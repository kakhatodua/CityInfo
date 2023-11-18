using AutoMapper;
using CityInfo.API.Models;
using CityInfo.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.Mvc;

namespace CityInfo.API.Controllers
{
    [Route("api/cities/{cityId}/pointsofinterest")]
    [Authorize(Policy = "MustBeFromAntwerp")]
    [ApiController]
    public class PointsOfInterestController : ControllerBase
    {
        private readonly ILogger<PointsOfInterestController> _logger;
        private readonly IMailService _mailService;
        private readonly IMapper _mapper;
        private readonly ICityInfoRepository _cityInfoRepository;
        public PointsOfInterestController(ILogger<PointsOfInterestController> logger, IMailService mailService, ICityInfoRepository cityInfoRepository, IMapper mapper )
        {
            if (mailService is null)
            {
                throw new ArgumentNullException(nameof(mailService));
            }

            if (cityInfoRepository is null)
            {
                throw new ArgumentNullException(nameof(cityInfoRepository));
            }

            if (mapper is null)
            {
                throw new ArgumentNullException(nameof(mapper));
            }

            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
         
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<PointOfInterestDto>>> GetPointsOfInterest(int cityId)
        {
            var cityName = User.Claims.FirstOrDefault(c => c.Type == "city")?.Value;
            if(!await _cityInfoRepository.CityNameMatchesCityId(cityName, cityId))
            {
                return Forbid();
            }  

            if(!await _cityInfoRepository.CityExistsAsync(cityId))
            {
                _logger.LogInformation($"City with id {cityId} was not found when accessing points of interest.");
                return NotFound();
            }
            var pointsOfInterestForCity = await _cityInfoRepository.GetPointOfInterestForCityAsync(cityId);
            return Ok(_mapper.Map<IEnumerable<PointOfInterestDto>>(pointsOfInterestForCity));
        }

        [HttpGet("{pointofinterestid}", Name = "GetPointOfInterest")]
        public async Task<ActionResult<PointOfInterestDto>> GetPointOfInterest(int cityId, int poinOfInterestId)
        {
           if(!await _cityInfoRepository.CityExistsAsync(cityId))
            {
                return NotFound();
            }

           var pointOfInterest = await _cityInfoRepository.GetPointOfInterestForCityAsync(cityId, poinOfInterestId);

            if(pointOfInterest == null)
            {
                return NotFound();
            }

            return Ok(_mapper.Map<PointOfInterestDto>(pointOfInterest));    
        }
        
        [HttpPost] 
        public async Task<ActionResult<PointOfInterestDto>> CreatePointOfInterest(int cityId, [FromBody] PointOfInterestForCreationDto pointOfInterest)
        {
           if(!await _cityInfoRepository.CityExistsAsync(cityId))
            {
                return NotFound();
            } 
           
            var finalPointOfInterest = _mapper.Map<Entities.PointOfInterest>(pointOfInterest);
           
            await _cityInfoRepository.AddPointOfInterestForCityAsync(cityId, finalPointOfInterest);

            await _cityInfoRepository.SaveChangesAsync();

            return CreatedAtRoute("GetPointOfInterest",
                  new
                  {
                      cityId = cityId,
                      pointOfInterestId = createdPointOfInterestToReturn.Id
                  },
                  createdPointOfInterestToReturn);

        }
        
        [HttpPut("{PointOfInterestId}")]
        public async Task<ActionResult> UpdatePointOfInterest(int cityId, int PointOfInterestId, PointOfInterestForUpdateDto pointOfInterest)
        {
            if(!await _cityInfoRepository.CityExistsAsync(cityId))
            {
                return NotFound();
            }

            var pointOfInterestEntity = await _cityInfoRepository.GetPointOfInterestForCityAsync(cityId, PointOfInterestId);
            if(pointOfInterestEntity == null)
            {
                return NotFound();
            }
            _mapper.Map(pointOfInterest, pointOfInterestEntity);
            await _cityInfoRepository.SaveChangesAsync();

            return NoContent();
        }
        
        [HttpPatch("{pointOfInterestId}")]
        public async Task<ActionResult> PartiallyUpdatePointOfInterest(int cityId, int pointOfInterestId, JsonPatchDocument<PointOfInterestForUpdateDto> patchDocument)
        {
            if(!await _cityInfoRepository.CityExistsAsync(cityId))
            {
                return NotFound();
            }

            var pointOfInterestEntity = await _cityInfoRepository.GetPointOfInterestForCityAsync(cityId, pointOfInterestId);
            if (pointOfInterestEntity == null)
            {
                return NotFound();
            }
            
            var pointOfInterestToPatch = _mapper.Map<PointOfInterestForUpdateDto>(pointOfInterestEntity);

            patchDocument.ApplyTo(pointOfInterestToPatch, ModelState);

            if (ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (!TryValidateModel(pointOfInterestToPatch))
            {
                return BadRequest(ModelState);
            }

            _mapper.Map(pointOfInterestToPatch, pointOfInterestEntity);
            await _cityInfoRepository.SaveChangesAsync();

            return NoContent();


        }
        
        [HttpDelete("{pointOfInterestId}")]
        public async Task<ActionResult> DeletePointOfInterest(int cityId, int pointOfInterestId)
        {
            if(!await _cityInfoRepository.CityExistsAsync(cityId))
            {
                return NotFound();
            }

            var pointOfInerestEntity = await _cityInfoRepository.GetPointOfInterestForCityAsync(cityId, pointOfInterestId);
            if(pointOfInerestEntity == null)
            {
                return NotFound();
            }

            _cityInfoRepository.DeletePointOfInterest(pointOfInerestEntity); 
            await _cityInfoRepository.SaveChangesAsync();   
            
            _mailService.Send(
                "point of interest deleted.", $"point of interest {pointOfInerestEntity.Name} with id {pointOfInerestEntity.Id} was deleted");

            return NoContent(); 
        }
    }
}
